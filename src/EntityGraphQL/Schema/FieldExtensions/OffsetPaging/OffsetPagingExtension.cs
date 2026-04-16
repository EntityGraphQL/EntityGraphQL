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
    /// True when the resolver interleaves service calls with context expressions
    /// (e.g. ctx.People.Where(p => service.Filter(p))).
    /// In this case the service and DB calls cannot be split across two passes,
    /// so the field executes in the first pass with the service included.
    /// </summary>
    public bool IsInterleavedServiceField { get; private set; }

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

        // Save the resolve expression BEFORE AddArguments, which may replace it with a new
        // object when the field already has arguments (arg params are merged/renamed).
        // We need this pre-merge reference to detect interleaved service fields below.
        var preAddArgsExpression = field.ResolveExpression;

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

        // Detect interleaved service: ExpressionExtractor extracts the whole resolver expression
        // when a service is used inside the resolver body (e.g. ctx.People.Where(service.Filter)).
        // In that case we cannot split into two passes and fall back to single-pass execution.
        // Compare against preAddArgsExpression because AddArguments may have replaced the expression
        // with a new object (when the field already had arguments whose params were merged/renamed).
        IsInterleavedServiceField = field.ExtractedFieldsFromServices?.Any(f => f.FieldExpressions.Any(e => ReferenceEquals(e, preAddArgsExpression))) ?? false;

        if (IsInterleavedServiceField && field.ExtractedFieldsFromServices != null && field.FieldParam != null)
        {
            field.ExtractedFieldsFromServices.Clear();
            field.ExtractedFieldsFromServices.AddRange(BuildInterleavedServiceInputs(schema, field, OriginalFieldExpression));
        }

        // if they have 2 fields with the type and paging we don't want to add extension multiple times
        // See OffsetPagingTests.TestMultiUseWithArgs
        if (!itemsField.Extensions.Any(e => e is OffsetPagingItemsExtension))
            itemsField.AddExtension(new OffsetPagingItemsExtension(isQueryable, listType!));

        // set up the field's expression so the types are all good
        var fieldExpression = BuildTotalCountExpression(null, returnType, field.ResolveExpression, field.ArgumentsParameter!);
        field.UpdateExpression(fieldExpression);
    }

    private static List<GraphQLExtractedField> BuildInterleavedServiceInputs(ISchemaProvider schema, IField field, Expression? fieldExpression)
    {
        var fieldParam = field.FieldParam ?? throw new EntityGraphQLSchemaException($"Field {field.Name} must have a field parameter");
        var extracted = new List<GraphQLExtractedField> { new(schema, BuildExtractedName($"{fieldParam}_ctx"), [fieldParam], fieldParam) };

        var baseCollection = FindBaseCollectionExpression(fieldExpression!);
        if (!ReferenceEquals(baseCollection, fieldParam))
            extracted.Add(new(schema, BuildExtractedName(baseCollection.ToString()), [baseCollection], fieldParam));

        return extracted;
    }

    private static Expression FindBaseCollectionExpression(Expression expression)
    {
        Expression current = expression;
        while (current is MethodCallExpression methodCall)
        {
            current = methodCall.Object ?? methodCall.Arguments[0];
        }
        return current;
    }

    private static string BuildExtractedName(string value)
    {
        return "egql__" + new string(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
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
        bool withoutServiceFields,
        ParameterReplacer parameterReplacer,
        ParameterExpression? originalArgParam,
        CompileContext compileContext
    )
    {
        // For fields WITHOUT services: skip rebuild in second pass (paging was done in first pass).
        // For service-backed paging fields, GraphQLObjectProjectionField returns early from GetFieldExpression
        // in the first pass before calling Field.GetExpression, so this extension is never reached then.
        // Paging is therefore always built in the second pass for service-backed fields.
        if (servicesPass && field.Services.Count == 0)
            return (expression, originalArgParam, argumentParam, arguments);

        if (argumentParam == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"{nameof(OffsetPagingExtension)} requires argumentParams to be set");

        if (maxPageSize != null && arguments?.Take > maxPageSize.Value)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{fieldNode.Name}' - Argument take can not be greater than {maxPageSize}.");

        // other extensions expect to run on the collection not our new shape
        var newItemsExp = OriginalFieldExpression!;

        // In the services pass, replace context-dependent expressions (e.g. dir.Id → anonElem.egql__dir_Id)
        // so that the TotalItems expression uses the correct values from the anonymous type.
        // Note: `context` here is the OffsetPage<T> MemberInit (already processed by ReplaceContext),
        // not the anonymous element parameter. We look up the actual anonymous element via the parent
        // field node's NextFieldContext, which was stored by GraphQLListSelectionField in the second pass.
        if (servicesPass && field.Services.Count > 0)
        {
            compileContext.AddServices(field.Services);
            if (field.ExtractedFieldsFromServices != null)
            {
                var anonElement = fieldNode.ParentNode?.NextFieldContext is ParameterExpression parentNextCtx ? compileContext.GetFieldContextReplacement(parentNextCtx) : null;
                var replacementCtx = (Expression?)anonElement ?? context;
                var expReplacer = new ExpressionReplacer(field.ExtractedFieldsFromServices, replacementCtx, false, false, null);
                newItemsExp = expReplacer.Replace(newItemsExp);
                if (field.FieldParam != null)
                    newItemsExp = parameterReplacer.Replace(newItemsExp, field.FieldParam, replacementCtx);
            }
        }

        // update the context
        foreach (var extension in Extensions)
        {
            var res = extension.GetExpressionAndArguments(
                field,
                fieldNode,
                newItemsExp,
                argumentParam,
                arguments,
                context,
                servicesPass,
                withoutServiceFields,
                parameterReplacer,
                originalArgParam,
                compileContext
            );
            (newItemsExp, originalArgParam, argumentParam, arguments) = (res.Item1!, res.Item2, res.Item3!, res.Item4);
        }

        // Build our field expression and hold it for use in the next step
        Expression fieldExpression = BuildTotalCountExpression(fieldNode, returnType!, newItemsExp, argumentParam);
        return (fieldExpression, originalArgParam, argumentParam, arguments);
    }
}
