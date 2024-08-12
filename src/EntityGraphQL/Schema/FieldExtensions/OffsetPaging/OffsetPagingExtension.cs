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
            throw new EntityGraphQLCompilerException($"OffsetPagingExtension requires a Resolve function set on the field");

        if (!field.ResolveExpression.Type.IsEnumerableOrArray())
            throw new ArgumentException($"Expression for field {field.Name} must be a collection to use OffsetPagingExtension. Found type {field.ReturnType.TypeDotnet}");

        if (field.FieldType == GraphQLQueryFieldType.Mutation)
            throw new EntityGraphQLCompilerException($"OffsetPagingExtension cannot be used on a mutation field {field.Name}");

        listType =
            field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()
            ?? throw new ArgumentException($"Expression for field {field.Name} must be a collection to use OffsetPagingExtension. Found type {field.ReturnType.TypeDotnet}");

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

        field.Returns(SchemaBuilder.MakeGraphQlType(schema, false, returnType, page, field.Name, field.FromType));

        // Update field arguments
        field.AddArguments(new OffsetArgs());
        if (defaultPageSize.HasValue)
            field.Arguments["take"].DefaultValue = defaultPageSize.Value;

        isQueryable = typeof(IQueryable).IsAssignableFrom(field.ResolveExpression.Type);

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
        var fieldExpression = BuildTotalCountExpression(returnType, field.ResolveExpression, field.ArgumentsParameter!);
        field.UpdateExpression(fieldExpression);
    }

    private MemberInitExpression BuildTotalCountExpression(Type returnType, Expression resolve, ParameterExpression argumentParam)
    {
        var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", [listType!], resolve);

        var expression = Expression.MemberInit(
            Expression.New(
                returnType.GetConstructor([typeof(int), typeof(int?), typeof(int?)])!,
                totalCountExp,
                Expression.PropertyOrField(argumentParam!, "skip"),
                Expression.PropertyOrField(argumentParam!, "take")
            )
        );
        return expression;
    }

    public override Expression? GetExpression(
        IField field,
        Expression expression,
        ParameterExpression? argumentParam,
        dynamic? arguments,
        Expression context,
        IGraphQLNode? parentNode,
        bool servicesPass,
        ParameterReplacer parameterReplacer
    )
    {
        if (servicesPass)
            return expression; // we don't need to do anything. items field is there to handle it now

        if (argumentParam == null)
            throw new EntityGraphQLCompilerException($"OffsetPagingExtension requires argumentParams to be set");

        if (maxPageSize != null && arguments?.Take > maxPageSize.Value)
            throw new EntityGraphQLArgumentException($"Argument take can not be greater than {maxPageSize}.");

        // other extensions expect to run on the collection not our new shape
        var newItemsExp = OriginalFieldExpression!;
        // update the context
        foreach (var extension in Extensions)
        {
            newItemsExp = extension.GetExpression(field, newItemsExp, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer);
        }

        // Build our field expression and hold it for use in the next step
        var fieldExpression = BuildTotalCountExpression(returnType!, newItemsExp, argumentParam);
        return fieldExpression;
    }
}
