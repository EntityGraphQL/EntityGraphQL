using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
        Dictionary<string, object>? arguments
    )
        : base(schema, field, name, nextFieldContext, rootParameter, context, arguments)
    {
        this.ListExpression = nodeExpression;
    }

    public GraphQLListSelectionField(GraphQLListSelectionField context, ParameterExpression? nextFieldContext)
        : base(context, nextFieldContext)
    {
        this.ListExpression = context.ListExpression;
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
        List<GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        object? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        if (withoutServiceFields && IsRootField && HasServices)
            return null;

        var listContext = HandleBulkServiceResolver(compileContext, withoutServiceFields, ListExpression)!;

        ParameterExpression? nextFieldContext = (ParameterExpression)NextFieldContext!;
        if (contextChanged && replacementNextFieldContext != null)
        {
            listContext = ReplaceContext(replacementNextFieldContext!, replacer, listContext!, possibleNextContextTypes);
            nextFieldContext = Expression.Parameter(listContext.Type.GetEnumerableOrArrayType()!, $"{nextFieldContext.Name}2");
        }
        (listContext, var argumentParams) =
            Field?.GetExpression(listContext!, replacementNextFieldContext, ParentNode!, schemaContext, compileContext, Arguments, docParam, docVariables, Directives, contextChanged, replacer)
            ?? (ListExpression, null);
        if (listContext == null)
            return null;

        HandleBeforeRootFieldExpressionBuild(compileContext, GetOperationName(this), Name, contextChanged, IsRootField, ref listContext);

        (listContext, var newNextFieldContext) = ProcessExtensionsPreSelection(listContext, nextFieldContext, replacer);
        if (newNextFieldContext != null)
            nextFieldContext = newNextFieldContext;

        var selectionFields = GetSelectionFields(compileContext, serviceProvider, fragments, docParam, docVariables, withoutServiceFields, nextFieldContext, schemaContext, contextChanged, replacer);

        if (selectionFields == null || selectionFields.Count == 0)
        {
            if (withoutServiceFields && HasServices)
                return null;
            return listContext;
        }

        (listContext, selectionFields, nextFieldContext) = ProcessExtensionsSelection(listContext, selectionFields, nextFieldContext, argumentParams, contextChanged, replacer);

        if (HasServices)
            compileContext.AddServices(Field!.Services);

        Expression? resultExpression = null;
        if (!withoutServiceFields)
        {
            bool needsServiceWrap = NeedsServiceWrap(withoutServiceFields);
            if (needsServiceWrap)
            {
                // To support a common use case where we are coming from a service result to another service field where the
                // service is the Query Context. Which we are assuming is likely an EF context and we don't need the null check
                // Use ExecutionOptions.ExecuteServiceFieldsSeparately = false to disable this behavior
                var nullCheck = Field!.Services.Any(s => s.Type != Field.Schema.QueryContextType);
                (resultExpression, PossibleNextContextTypes) = ExpressionUtil.MakeSelectWithDynamicType(this, nextFieldContext!, listContext, selectionFields, nullCheck, withoutServiceFields);
            }
        }

        var useSelectWithNullCheck = contextChanged || !compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately || HasServices;
        // have this return both the dynamic types so we can use them next, post-service
        if (resultExpression == null)
            (resultExpression, PossibleNextContextTypes) = ExpressionUtil.MakeSelectWithDynamicType(
                this,
                nextFieldContext!,
                listContext,
                selectionFields,
                useSelectWithNullCheck,
                withoutServiceFields || !contextChanged
            );

        var resultElementType = resultExpression.Type.GetEnumerableOrArrayType()!;

        // Make sure lists are evaluated and not deferred otherwise the second pass with services will fail if it needs to wrap for null check above
        // root level is handled in ExecutableGraphQLStatement with a null check
        if (AllowToList && !IsRootField && resultExpression.Type.IsEnumerableOrArray() && !resultExpression.Type.IsDictionary())
            resultExpression = Expression.Call(
                typeof(EnumerableExtensions),
                nameof(EnumerableExtensions.ToListWithNullCheck),
                [resultElementType],
                resultExpression,
                Expression.Constant(Field!.ReturnType.TypeNotNullable)
            );

        // make sure we null check the object we are calling. Only needed if this is not the final call as
        // otherwise we use a SelectWithNullCheck to avoid double service call if we are coming from a service
        // if (!useSelectWithNullCheck)
        // {
        //     // could be EF context, needs to be something it can handle
        //     Expression nullResult = Expression.Constant(null, resultExpression.Type);
        //     var nullCheckResultType = resultExpression.Type;
        //     if (Field?.ReturnType.TypeNotNullable == true)
        //     {
        //         nullResult = Expression.New(typeof(List<>).MakeGenericType(resultElementType));
        //         var enumerableType = typeof(IEnumerable<>).MakeGenericType(resultElementType);
        //         nullResult = Expression.Convert(nullResult, enumerableType);
        //         nullCheckResultType = enumerableType;
        //     }
        //     resultExpression = Expression.Condition(Expression.Equal(listContext, Expression.Constant(null, listContext.Type)), nullResult, resultExpression, nullCheckResultType);
        // }

        return resultExpression;
    }

    protected override ParameterExpression? HandleBulkResolverForField(
        CompileContext compileContext,
        BaseGraphQLField field,
        IBulkFieldResolver bulkResolver,
        ParameterExpression? docParam,
        object? docVariables,
        ParameterReplacer replacer
    )
    {
        return DefaultHandleBulkResolverForField(compileContext, field, bulkResolver, docParam, docVariables, replacer);
    }
}
