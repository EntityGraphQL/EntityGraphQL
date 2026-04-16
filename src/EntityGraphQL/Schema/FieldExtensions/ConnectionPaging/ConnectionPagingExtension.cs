using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Sets up a few extensions to modify a simple collection expression - db.Movies.OrderBy() into a connection paging graph
/// </summary>
public class ConnectionPagingExtension : BaseFieldExtension
{
    private Type? listType;
    private bool isQueryable;
    private Type? returnType;

    public Expression? OriginalFieldExpression { get; private set; }
    public int? DefaultPageSize { get; }
    public int? MaxPageSize { get; }
    public List<IFieldExtension> ExtensionsBeforePaging { get; private set; } = [];

    /// <summary>
    /// True when the resolver interleaves service calls with context expressions
    /// (e.g. ctx.People.Where(p => service.Filter(p))).
    /// In this case the service and DB calls cannot be split across two passes,
    /// so the field executes in the first pass with the service included.
    /// </summary>
    public bool IsInterleavedServiceField { get; private set; }

    public ConnectionPagingExtension(int? defaultPageSize, int? maxPageSize)
    {
        DefaultPageSize = defaultPageSize;
        MaxPageSize = maxPageSize;
    }

    /// <summary>
    /// Configure the field for a connection style paging field. Do as much as we can here as it is only executed once.
    ///
    /// There are a few fun things happening.
    ///
    /// 1. In this extension we set up the field with the Connection<T> object graph using the constructor to implement most
    ///    of the fields
    /// 2. We set up an extension on this field.edges.node to capture the selection from the compiled query as node is the <T>
    ///    they are selecting fields from
    /// 3. We set up an extension of field.edges which using data from this extension (we get the context and the args) and the
    ///    field.edges.node Select() to build a EF compatible expression that only returns the fields asked for in edges.node
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="field"></param>
    public override void Configure(ISchemaProvider schema, IField field)
    {
        if (field.ResolveExpression == null)
            throw new EntityGraphQLSchemaException($"ConnectionPagingExtension requires a Resolve function set on the field");

        if (!field.ResolveExpression.Type.IsEnumerableOrArray())
            throw new EntityGraphQLSchemaException($"Expression for field {field.Name} must be a collection to use ConnectionPagingExtension. Found type {field.ReturnType.TypeDotnet}");

        // Make sure required types are in the schema
        if (!schema.HasType(typeof(ConnectionPageInfo)))
            schema.AddType<ConnectionPageInfo>("PageInfo", "Metadata about a page of data").AddAllFields();
        listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()!;
        isQueryable = field.ReturnType.TypeDotnet.IsGenericTypeQueryable();

        var edgeType = typeof(ConnectionEdge<>).MakeGenericType(listType);
        if (!schema.HasType(edgeType))
        {
            var edgeName = $"{field.ReturnType.SchemaType.Name}Edge";
            schema.AddType(edgeType, edgeName, "Metadata about an edge of page result").AddAllFields();
        }

        ISchemaType returnSchemaType;
        var connectionType = typeof(Connection<>).MakeGenericType(listType);
        var connectionName = $"{field.ReturnType.SchemaType.Name}Connection";
        if (!schema.HasType(connectionType))
        {
            returnSchemaType = schema.AddType(connectionType, connectionName, $"Metadata about a {field.ReturnType.SchemaType.Name} connection (paging over people)").AddAllFields();
        }
        else
        {
            returnSchemaType = schema.Type(connectionName);
        }
        returnType = returnSchemaType.TypeDotnet;

        field.Returns(SchemaBuilder.MakeGraphQlType(schema, false, returnType, returnSchemaType, field.Name, field.FromType));

        // Save the resolve expression BEFORE AddArguments, which may replace it with a new
        // object when the field already has arguments (arg params are merged/renamed).
        // We need this pre-merge reference to detect interleaved service fields below.
        var preAddArgsExpression = field.ResolveExpression;

        // Update field arguments
        field.AddArguments(new ConnectionArgs());

        // set up Extension on Edges.Node field to handle the Select() insertion
        var edgesField = returnSchemaType.GetField(schema.SchemaFieldNamer("Edges"), null);

        // We steal any previous extensions as they were expected to work on the original Resolve which we moved to Edges
        ExtensionsBeforePaging = field.Extensions.Take(field.Extensions.FindIndex(e => e is ConnectionPagingExtension)).ToList();
        // the remaining extensions expect to be built from the ConnectionPaging shape
        field.Extensions = field.Extensions.Skip(ExtensionsBeforePaging.Count).ToList();
        // We use this extension to update the Edges context by inserting the Select() which we get from the above extension
        // if they have 2 fields with the type and paging we don't want to add extension multiple times
        // See OffsetPagingTests.TestMultiUseWithArgs
        if (!edgesField.Extensions.Any(e => e is ConnectionEdgeExtension))
            edgesField.AddExtension(new ConnectionEdgeExtension(listType, isQueryable));

        OriginalFieldExpression = field.ResolveExpression;

        // Detect interleaved service: ExpressionExtractor extracts the whole resolver expression
        // (not just sub-expressions) when a service is used inside the resolver body
        // e.g. ctx.People.Where(p => service.Filter(p)).OrderBy(p => p.Id)
        // In that case we cannot split into two passes and fall back to single-pass execution.
        // Compare against preAddArgsExpression because AddArguments may have replaced the expression
        // with a new object (when the field already had arguments whose params were merged/renamed).
        IsInterleavedServiceField = field.ExtractedFieldsFromServices?.Any(f => f.FieldExpressions.Any(e => ReferenceEquals(e, preAddArgsExpression))) ?? false;

        if (IsInterleavedServiceField && field.ExtractedFieldsFromServices != null && field.FieldParam != null)
        {
            field.ExtractedFieldsFromServices.Clear();
            field.ExtractedFieldsFromServices.AddRange(BuildInterleavedServiceInputs(schema, field, OriginalFieldExpression));
        }

        // Rebuild expression so all the fields and types are known
        // and get it ready for completion at runtime (we need to know the selection fields to complete)
        // it is built to reduce redundant repeated expressions. The whole thing ends up in a null check wrap
        // conceptually it does similar to below (using Demo context)
        // See Connection for implementation details of TotalCount and PageInfo
        // (ctx, arguments) => {
        //      var connection = new Connection<Person>(arguments)
        //      {
        //          TotalCount = ctx.Actors.Select(a => a.Person).Count(), // only if needed
        //          Edges = ctx.Actors.Select(a => a.Person)
        //              -- other extensions might do things here (e.g. filter / sort)
        //              .Skip(GetSkipNumber(arguments))
        //              .Take(GetTakeNumber(arguments))
        //              // we insert Select() here so that we do not fetch the whole table if using EF
        //              .Select(a => new ConnectionEdge<Person>
        //              {
        //                  Node = new {
        //                      field1 = a.field1,
        //                      ...
        //                 },
        //                 Cursor = null // built below
        //              })
        //              // this is the select in memory that lets us build the cursors
        //              .Select((a, idx) => new ConnectionEdge<Person> // this is from Enumerable and EF will run the above
        //              {
        //                  Node = a,
        //                  Cursor = ConnectionHelper.GetCursor(arguments, idx)
        //              }),
        //      };
        //      if (connection == null)
        //          return null;
        //      return .... // does the select of only the Connection fields asked for
        // need to set this up here as the types are needed as we visiting the query tree
        // we build the real one below in GetExpression()
        var fieldExpression = BuildConnectionExpression(null, null, OriginalFieldExpression!, field.ArgumentsParameter!);
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

    private MemberInitExpression BuildConnectionExpression(BaseGraphQLField? fieldNode, dynamic? arguments, Expression resolve, ParameterExpression argumentParam)
    {
        // Check if we need to compute totalCount:
        // 1. totalCount field is selected
        // 2. pageInfo field is selected (all pageInfo fields depend on totalCount)
        // 3. 'last' argument is used (skip calculation needs totalCount when last is used without before)
        var needsCount = fieldNode?.QueryFields?.Any(f => f.Field?.Name == "totalCount" || f.Field?.Name == "pageInfo") ?? true;

        // Also need count if 'last' argument is provided (for skip/cursor calculations)
        if (!needsCount && arguments?.Last != null)
            needsCount = true;

        var bindings = new List<MemberBinding>();
        if (needsCount)
        {
            var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), nameof(Enumerable.Count), [listType!], resolve);
            bindings.Add(Expression.Bind(returnType!.GetProperty("TotalCount")!, totalCountExp));
        }

        return Expression.MemberInit(Expression.New(returnType!.GetConstructor([argumentParam.Type])!, argumentParam), bindings);
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

#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(argumentParam, nameof(argumentParam));
#else
        if (argumentParam == null)
            throw new ArgumentNullException(nameof(argumentParam));
#endif

        var edgeExpression = OriginalFieldExpression!;

        // In the services pass, replace context-dependent expressions (e.g. dir.Id → anonElem.egql__dir_Id)
        // so that the TotalCount expression uses the correct values from the anonymous type.
        // Note: `context` here is the Connection<T> MemberInit (already processed by ReplaceContext),
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
                edgeExpression = expReplacer.Replace(edgeExpression);
                if (field.FieldParam != null)
                    edgeExpression = parameterReplacer.Replace(edgeExpression, field.FieldParam, replacementCtx);
            }
        }

        if (ExtensionsBeforePaging.Count > 0)
        {
            // if we have other extensions (filter etc) we need to apply them to the totalCount
            foreach (var extension in ExtensionsBeforePaging)
            {
                var res = extension.GetExpressionAndArguments(
                    field,
                    fieldNode,
                    edgeExpression,
                    argumentParam,
                    arguments,
                    context,
                    servicesPass,
                    withoutServiceFields,
                    parameterReplacer,
                    originalArgParam,
                    compileContext
                );
                (edgeExpression, originalArgParam, argumentParam, arguments) = (res.Item1!, res.Item2, res.Item3!, res.Item4);
            }
        }

        expression = BuildConnectionExpression(fieldNode, arguments, edgeExpression, argumentParam);
        return (expression, originalArgParam, argumentParam, arguments);
    }
}
