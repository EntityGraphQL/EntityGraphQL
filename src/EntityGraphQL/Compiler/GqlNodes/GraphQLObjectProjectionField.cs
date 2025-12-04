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
/// Represents a field node in the GraphQL query. That operates on a single object.
/// query MyQuery {
///     people {
///         id, name
///     }
///     customer { # GraphQLObjectProjectionField
///         id
///     }
/// }
///
/// Builds an expression like
/// ctx => new { Id = ctx.Customer.Id }
/// </summary>
public class GraphQLObjectProjectionField : BaseGraphQLQueryField
{
    /// <summary>
    /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
    /// </summary>
    /// <param name="name">Name of the field</param>
    /// <param name="nextFieldContext">The next context expression for ObjectProjection is also our field expression e..g person.manager</param>
    /// <param name="rootParameter">The root parameter</param>
    /// <param name="parentNode"></param>
    public GraphQLObjectProjectionField(
        ISchemaProvider schema,
        IField? field,
        string name,
        Expression nextFieldContext,
        ParameterExpression rootParameter,
        IGraphQLNode parentNode,
        IReadOnlyDictionary<string, object?>? arguments
    )
        : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments) { }

    public GraphQLObjectProjectionField(GraphQLObjectProjectionField context, Expression? nextFieldContext)
        : base(context, nextFieldContext) { }

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
        var nextFieldContext = HandleBulkServiceResolver(compileContext, withoutServiceFields, NextFieldContext);
        if (HasServices && withoutServiceFields)
            return Field
                ?.ExtractedFieldsFromServices?.FirstOrDefault()
                ?.GetNodeExpression(
                    compileContext,
                    serviceProvider,
                    fragments,
                    docParam,
                    docVariables,
                    schemaContext,
                    withoutServiceFields,
                    replacementNextFieldContext,
                    possibleNextContextTypes,
                    contextChanged,
                    replacer
                );

        if (contextChanged && replacementNextFieldContext != null)
        {
            nextFieldContext = ReplaceContext(replacementNextFieldContext!, replacer, nextFieldContext!, possibleNextContextTypes);
        }
        (nextFieldContext, var argumentParam) =
            Field?.GetExpression(nextFieldContext!, replacementNextFieldContext, this, schemaContext, compileContext, Arguments, docParam, docVariables, Directives, contextChanged, replacer)
            ?? (nextFieldContext, null);
        if (nextFieldContext == null)
            return null;

        HandleBeforeRootFieldExpressionBuild(compileContext, GetOperationName(this), Name, contextChanged, IsRootField, ref nextFieldContext);

        (nextFieldContext, _) = ProcessExtensionsPreSelection(nextFieldContext, null, replacer);

        // if we have services and they don't want service fields, return the expression only for extraction
        if (withoutServiceFields && HasServices && !IsRootField)
            return nextFieldContext;

        var selectionContext = nextFieldContext;
        bool needsServiceWrap = NeedsServiceWrap(withoutServiceFields) || ((nextFieldContext.NodeType == ExpressionType.MemberInit || nextFieldContext.NodeType == ExpressionType.New) && IsRootField);

        if (Field?.IsAsync == true && !contextChanged)
        {
            // for async fields we need to build the selection on the result of the task
            var resultType = nextFieldContext.Type.GetGenericArguments()[0];
            selectionContext = Expression.Parameter(resultType, $"{Name}_result");
        }
        else if (needsServiceWrap)
        {
            // we need to build the selection on a parameter of the result type
            selectionContext = Expression.Parameter(nextFieldContext.Type, $"{Name}_result");
        }
        var selectionFields = GetSelectionFields(compileContext, serviceProvider, fragments, docParam, docVariables, withoutServiceFields, selectionContext, schemaContext, contextChanged, replacer);
        if (selectionFields == null || selectionFields.Count == 0)
            return null;

        if (HasServices)
            compileContext.AddServices(Field!.Services);

        // build a new {...} - returning a single object {}
        (nextFieldContext, selectionFields, _) = ProcessExtensionsSelection(nextFieldContext, selectionFields, null, argumentParam, contextChanged, replacer);
        var newExp = ExpressionUtil.CreateNewExpressionWithInterfaceOrUnionCheck(Name, nextFieldContext, Field!.ReturnType, selectionFields, out Type anonType)!;

        if (needsServiceWrap || Field?.IsAsync == true)
        {
            nextFieldContext = Expression.Call(
                typeof(EnumerableExtensions),
                nameof(EnumerableExtensions.ProjectWithNullCheck),
                [selectionContext.Type, anonType],
                nextFieldContext,
                Expression.Lambda(newExp, (ParameterExpression)selectionContext)
            );
        }
        else
        {
            var isNullable = !nextFieldContext.Type.IsValueType || nextFieldContext.Type.IsNullableType();
            if (isNullable && nextFieldContext.NodeType != ExpressionType.MemberInit && nextFieldContext.NodeType != ExpressionType.New)
            {
                // make a null check from this new expression
                nextFieldContext = Expression.Condition(
                    Expression.MakeBinary(ExpressionType.Equal, nextFieldContext, Expression.Constant(null)),
                    Expression.Constant(null, anonType),
                    newExp!,
                    anonType
                );
            }
            else
            {
                nextFieldContext = newExp;
            }
        }

        return nextFieldContext;
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
