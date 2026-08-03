using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

/// <summary>
/// Represents a field node in the GraphQL query. That operates on a list of things.
/// query MyQuery {
///     people { # GraphQLListSelectionField
///         id, name
///     }
///     person(id: "") { id }
/// }
/// </summary>
public class GraphQLListSelectionField : BaseGraphQLQueryField
{
    public bool AllowToList { get; set; } = true;
    public Expression ListExpression { get; set; }

    /// <summary>
    /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
    /// </summary>
    /// <param name="schema">The Schema Provider that defines the GraphQL schema</param>
    /// <param name="field">Field from the schema that this GraphQLListSelectionField is built from</param>
    /// <param name="name">Name of the field. Could be the alias that the user provided</param>
    /// <param name="nextFieldContext">A context for a field building on this. This will be the list element parameter</param>
    /// <param name="rootParameter">Root parameter used by this nodeExpression (movie in example above).</param>
    /// <param name="nodeExpression">Expression for the list</param>
    /// <param name="context">Parent node</param>
    /// <param name="arguments"></param>
    public GraphQLListSelectionField(
        ISchemaProvider schema,
        IField? field,
        string name,
        ParameterExpression? nextFieldContext,
        ParameterExpression? rootParameter,
        Expression nodeExpression,
        IGraphQLNode context,
        Dictionary<string, object?>? arguments
    )
        : base(schema, field, name, nextFieldContext, rootParameter, context, arguments)
    {
        ListExpression = nodeExpression;
    }

    public GraphQLListSelectionField(GraphQLListSelectionField context, ParameterExpression? nextFieldContext)
        : base(context, nextFieldContext)
    {
        ListExpression = context.ListExpression;
        AllowToList = context.AllowToList;
    }

    /// <summary>
    /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
    /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
    /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
    /// Execute() so we can look up any query fragment selections
    /// </summary>
    protected override Expression? GetFieldExpression(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        // A root service list returns null here so the statement falls through to a single-pass execution -
        // there is no context query for a first pass to run. The exception is a selection with a bulk
        // resolver in it: those need a materialised row set to collect their keys from, so the list takes
        // part in the two passes instead (run the service selecting non-service / extracted key fields, run
        // the bulk loaders, then project the full selection from that result). Doing this unconditionally
        // costs an extra compile and a projection of every row for selections with nothing to bulk load.
        if (withoutServiceFields && IsRootField && HasServices)
        {
            if (!HasBulkResolverAtOrBelow(fragments))
                return null;
            // taking part in the two passes: the second one projects from what this one materializes
            compileContext.SetFirstPassMaterialized(this);
        }

        var listContext = HandleBulkServiceResolver(compileContext, withoutServiceFields, ListExpression)!;

        ParameterExpression? nextFieldContext = (ParameterExpression)NextFieldContext!;
        if (contextChanged && replacementNextFieldContext != null)
        {
            // The first pass materialized this field's own projection, so use it. Rebuilding from the service
            // expression would run the service a second time and leave nested selections on the entity type
            // while possibleNextContextTypes point at the first pass's dynamic type.
            listContext = UseFirstPassResult(compileContext, replacementNextFieldContext)
                ? replacementNextFieldContext
                : ReplaceContext(replacementNextFieldContext!, replacer, listContext!, possibleNextContextTypes);
            // For async fields (e.g. Task<IEnumerable<T>>), GetEnumerableOrArrayType returns
            // IEnumerable<T> (the Task's single type argument) instead of T (the list element type).
            // Unwrap Task<>/ValueTask<> first so the element parameter has the correct type.
            // IAsyncEnumerable<T> must NOT be unwrapped here — its single generic argument IS already
            // the element type, so GetEnumerableOrArrayType() on IAsyncEnumerable<T> returns T directly.
            // Use IsAwaitableGenericType() rather than IsAsyncGenericType() for this reason.
            var listContextType = listContext.Type;
            if (listContextType.IsAwaitableGenericType())
                listContextType = listContextType.GetGenericArguments()[0];
            // GetEnumerableOrArrayType() returns null for a type that is enumerable but not itself generic/array
            // (e.g. a CLR type mapped to a list GraphQL type via AddTypeMapping, like NpgsqlPolygon -> [Point!]!).
            // In that case the element type has not changed from parse time, so fall back to the original
            // element parameter type rather than passing null to Expression.Parameter.
            var elementType = listContextType.GetEnumerableOrArrayType() ?? nextFieldContext.Type;
            nextFieldContext = Expression.Parameter(elementType, $"{nextFieldContext.Name}2");
            // Store replacement so child paging extensions (e.g. ConnectionEdgeExtension) can get
            // the correct anonymous-type element parameter when building service expressions.
            if (NextFieldContext is ParameterExpression origNextCtx)
                compileContext.SetFieldContextReplacement(origNextCtx, nextFieldContext);
        }
        (listContext, var argumentParams) =
            Field?.GetExpression(
                listContext!,
                replacementNextFieldContext,
                this,
                schemaContext,
                compileContext,
                Arguments,
                docParam,
                docVariables,
                Directives,
                contextChanged,
                withoutServiceFields,
                replacer
            ) ?? (ListExpression, null);
        if (listContext == null)
            return null;

        HandleBeforeRootFieldExpressionBuild(compileContext, GetOperationName(this), Name, contextChanged, IsRootField, ref listContext);

        (listContext, var newNextFieldContext) = ProcessExtensionsPreSelection(listContext, nextFieldContext, replacer);
        if (newNextFieldContext != null)
            nextFieldContext = newNextFieldContext;

        var selectionFields = GetSelectionFields(compileContext, serviceProvider, fragments, docParam, docVariables, withoutServiceFields, nextFieldContext, schemaContext, contextChanged, replacer);

        if (HasServices)
            compileContext.AddServices(Field!.Services);

        if (selectionFields == null || selectionFields.Count == 0)
        {
            if (withoutServiceFields && HasServices)
                return null;
            return listContext;
        }

        (listContext, selectionFields, nextFieldContext) = ProcessExtensionsSelection(listContext, selectionFields, nextFieldContext, argumentParams, contextChanged, replacer);

        var isAsync = Field?.IsAsync == true;
        var useNullCheckMethods =
            contextChanged || !compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately || HasServices || Field?.Services.Any(s => s.Type != Field.Schema.QueryContextType) == true;
        // The projection is a different decision to the materialisation below. A list field resolved from the
        // query context itself - e.g. .Resolve<MyDbContext>((c, db) => db.Things.Where(t => t.ParentId == c.Id))
        // for a child collection that is not a navigation property - is assumed to be a database query, and its
        // selection has to stay a plain Select so the provider can translate it. SelectWithNullCheck is
        // EntityGraphQL's own method: harmless in memory, but EF Core rejects the whole query when it appears
        // inside one ("The LINQ expression 'p_Thing => new Dynamic_things{...}' could not be translated"), which
        // is what happens as soon as such a field is selected below another one of the same kind. Services other
        // than the context are client-side and do need the null check - a service can return null.
        // Set ExecutionOptions.ExecuteServiceFieldsSeparately = false to opt out of this behaviour.
        var useNullCheckOnProjection = !withoutServiceFields && HasServices ? Field!.Services.Any(s => s.Type != Field.Schema.QueryContextType) : useNullCheckMethods;
        // have this return both the dynamic types so we can use them next, post-service. They are stored on the
        // compileContext (not this node) as the node is part of the cached document shared across requests
        var (resultExpression, nextContextTypes) = ExpressionUtil.MakeSelectWithDynamicType(
            this,
            nextFieldContext!,
            listContext,
            selectionFields,
            compileContext.GetPossibleNextContextTypes(this),
            useNullCheckOnProjection,
            isAsync,
            withoutServiceFields || !contextChanged
        );
        if (nextContextTypes != null)
            compileContext.SetPossibleNextContextTypes(this, nextContextTypes);

        var resultElementType = resultExpression.Type.GetEnumerableOrArrayType()!;

        // Lists must be evaluated in-tree, not deferred. Nested lists have no other point where evaluation
        // can be forced: a deferred iterator stored in the projected object breaks the second (services) pass,
        // which re-projects over the pass-1 result in memory (re-enumeration at the wrong time loses/duplicates
        // data), and leaks lazy iterators into the result graph where consumers expect List<T>.
        // ToListWithNullCheck additionally implements null source -> empty list for non-nullable list fields.
        // Exception: root fields on the database-bound pass (the plain ToList case) stay deferred - they have a
        // single consumer, ExecuteExpressionAsync, which materializes immediately, asynchronously when the LINQ
        // provider's query objects implement IAsyncEnumerable<T> (e.g. EF Core), so the database round-trip does
        // not block a thread and honours the request's CancellationToken
        var deferMaterializationToExecution = IsRootField && !contextChanged && !useNullCheckMethods;
        if (AllowToList && !deferMaterializationToExecution && Field?.IsAsync == false && resultExpression.Type.IsEnumerableOrArray() && !resultExpression.Type.IsDictionary())
            resultExpression = useNullCheckMethods
                ? Expression.Call(
                    typeof(EnumerableExtensions),
                    nameof(EnumerableExtensions.ToListWithNullCheck),
                    [resultElementType],
                    resultExpression,
                    Expression.Constant(Field!.ReturnType.TypeNotNullable)
                )
                : Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), [resultElementType], resultExpression);

        return resultExpression;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void HandleBulkResolverForField(
        CompileContext compileContext,
        BaseGraphQLField field,
        IBulkFieldResolver bulkResolver,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        ParameterReplacer replacer
    )
    {
        DefaultHandleBulkResolverForField(compileContext, field, bulkResolver, docParam, docVariables, replacer);
    }
}
