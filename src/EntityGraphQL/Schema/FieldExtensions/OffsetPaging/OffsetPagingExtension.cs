using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Sets up a few extensions to modify a collection expression (e.g. db.Movies.OrderBy()) into a paging graph
/// </summary>
public class OffsetPagingExtension : BaseFieldExtension
{
    public Expression? OriginalFieldExpression { get; private set; }
    public List<IFieldExtension> Extensions { get; private set; } = [];
    private bool isQueryable;
    private Type? listType;
    private Type? returnType;
    private readonly int? defaultPageSize;
    private readonly int? maxPageSize;

    public OffsetPagingExtension(int? defaultPageSize, int? maxPageSize)
    {
        this.defaultPageSize = defaultPageSize;
        this.maxPageSize = maxPageSize;
    }

    /// <summary>
    /// Configure the field for a offset style paging field. Do as much as we can here as it is only executed once.
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="field"></param>
    public override void Configure(ISchemaProvider schema, IField field)
    {
        if (field.ResolveExpression == null)
            throw new EntityGraphQLSchemaException($"{nameof(OffsetPagingExtension)} requires a Resolve function set on the field");

        if (!field.ResolveExpression.Type.IsEnumerableOrArray())
            throw new EntityGraphQLSchemaException($"Expression for field {field.Name} must be a collection to use {nameof(OffsetPagingExtension)}. Found type {field.ReturnType.TypeDotnet}");

        if (field.FieldType == GraphQLQueryFieldType.Mutation)
            throw new EntityGraphQLSchemaException($"{nameof(OffsetPagingExtension)} cannot be used on a mutation field {field.Name}");

        listType =
            field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()
            ?? throw new EntityGraphQLSchemaException($"Expression for field {field.Name} must be a collection to use {nameof(OffsetPagingExtension)}. Found type {field.ReturnType.TypeDotnet}");

        ISchemaType returnSchemaType;
        var page = $"{field.ReturnType.SchemaType.Name}Page";
        var pageType = typeof(OffsetPage<>).MakeGenericType(listType);
        if (!schema.HasType(pageType))
        {
            returnSchemaType = schema.AddType(pageType, page, $"Metadata about a {field.ReturnType.SchemaType.Name} page (paging over people)").AddAllFields();
        }
        else
        {
            returnSchemaType = schema.Type(page);
        }
        returnType = returnSchemaType.TypeDotnet;

        field.Returns(SchemaBuilder.MakeGraphQlType(schema, false, returnType, returnSchemaType, field.Name, field.FromType));

        // Update field arguments
        field.AddArguments(new OffsetArgs());
        if (defaultPageSize.HasValue)
            field.Arguments["take"].DefaultValue = new DefaultArgValue(true, defaultPageSize.Value);

        isQueryable = field.ResolveExpression.Type.IsGenericTypeQueryable();

        // We steal any previous extensions as they were expected to work on the original Resolve which we moved to Edges
        Extensions = field.Extensions.Take(field.Extensions.FindIndex(e => e is OffsetPagingExtension)).ToList();
        field.Extensions = field.Extensions.Skip(Extensions.Count).ToList();

        // update the Items field before we update the field.Resolve below
        var itemsField = returnSchemaType.GetField("items", null);
        OriginalFieldExpression = field.ResolveExpression!;
        // if they have 2 fields with the type and paging we don't want to add extension multiple times
        // See OffsetPagingTests.TestMultiUseWithArgs
        if (!itemsField.Extensions.Any(e => e is OffsetPagingItemsExtension))
            itemsField.AddExtension(new OffsetPagingItemsExtension(isQueryable, listType!));

        // set up the field's expression so the types are all good
        var fieldExpression = BuildTotalCountExpression(null, returnType, field.ResolveExpression, field.ArgumentsParameter!);
        field.UpdateExpression(fieldExpression);
    }

    private MemberInitExpression BuildTotalCountExpression(BaseGraphQLField? fieldNode, Type returnType, Expression resolve, ParameterExpression argumentParam)
    {
        var needsCount = fieldNode?.QueryFields?.Any(f => f.Field?.Name == "totalItems" || f.Field?.Name == "hasNextPage") ?? true;

        var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), nameof(Enumerable.Count), [listType!], resolve);

        var expression = Expression.MemberInit(
            Expression.New(returnType.GetConstructor([typeof(int?), typeof(int?)])!, Expression.PropertyOrField(argumentParam!, "skip"), Expression.PropertyOrField(argumentParam!, "take")),
            needsCount ? [Expression.Bind(returnType.GetProperty("TotalItems")!, totalCountExp)] : []
        );
        return expression;
    }

    public override (Expression? expression, ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue) GetExpressionAndArguments(
        IField field,
        BaseGraphQLField fieldNode,
        Expression expression,
        ParameterExpression? argumentParam,
        dynamic? arguments,
        Expression context,
        bool servicesPass,
        ParameterReplacer parameterReplacer,
        ParameterExpression? originalArgParam,
        CompileContext compileContext
    )
    {
        if (servicesPass)
            return (expression, originalArgParam, argumentParam, arguments);

        if (argumentParam == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"{nameof(OffsetPagingExtension)} requires argumentParams to be set");

        if (maxPageSize != null && arguments?.Take > maxPageSize.Value)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{fieldNode.Name}' - Argument take can not be greater than {maxPageSize}.");

        // other extensions expect to run on the collection not our new shape
        var newItemsExp = OriginalFieldExpression!;
        // update the context
        foreach (var extension in Extensions)
        {
            var res = extension.GetExpressionAndArguments(field, fieldNode, newItemsExp, argumentParam, arguments, context, servicesPass, parameterReplacer, originalArgParam, compileContext);
            (newItemsExp, originalArgParam, argumentParam, arguments) = (res.Item1!, res.Item2, res.Item3!, res.Item4);
        }

        // Build our field expression and hold it for use in the next step
        Expression fieldExpression = BuildTotalCountExpression(fieldNode, returnType!, newItemsExp, argumentParam);
        return (fieldExpression, originalArgParam, argumentParam, arguments);
    }
}
