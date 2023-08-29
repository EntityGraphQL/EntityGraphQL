using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
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
        public GraphQLObjectProjectionField(ISchemaProvider schema, IField? field, string name, Expression nextFieldContext, ParameterExpression rootParameter, IGraphQLNode parentNode, IReadOnlyDictionary<string, object>? arguments)
            : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments)
        {
        }

        public GraphQLObjectProjectionField(GraphQLObjectProjectionField context, Expression? nextFieldContext)
            : base(context, nextFieldContext)
        {
        }

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        protected override Expression? GetFieldExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            if (HasServices && withoutServiceFields)
                return Field?.ExtractedFieldsFromServices?.FirstOrDefault()?.GetNodeExpression(compileContext, serviceProvider, fragments, docParam, docVariables, schemaContext, withoutServiceFields, replacementNextFieldContext, isRoot, contextChanged, replacer);

            var nextFieldContext = NextFieldContext;

            if (contextChanged && replacementNextFieldContext != null)
            {
                nextFieldContext = ReplaceContext(replacementNextFieldContext!, isRoot, replacer, nextFieldContext!);
            }
            (nextFieldContext, var argumentParam) = Field?.GetExpression(nextFieldContext!, replacementNextFieldContext, ParentNode!, schemaContext, compileContext, Arguments, docParam, docVariables, Directives, contextChanged, replacer) ?? (nextFieldContext, null);
            if (nextFieldContext == null)
                return null;
            bool needsServiceWrap = NeedsServiceWrap(withoutServiceFields);

            (nextFieldContext, _) = ProcessExtensionsPreSelection(nextFieldContext, null, replacer);

            // if we have services and they don't want service fields, return the expression only for extraction
            if (withoutServiceFields && HasServices && !isRoot)
                return nextFieldContext;

            var selectionFields = GetSelectionFields(compileContext, serviceProvider, fragments, docParam, docVariables, withoutServiceFields, nextFieldContext, schemaContext, contextChanged, replacer);
            if (selectionFields == null || !selectionFields.Any())
                return null;

            if (HasServices)
                compileContext.AddServices(Field!.Services);

            if (needsServiceWrap ||
                ((nextFieldContext.NodeType == ExpressionType.MemberInit || nextFieldContext.NodeType == ExpressionType.New) && isRoot))
            {
                nextFieldContext = WrapWithNullCheck(compileContext, selectionFields, serviceProvider, nextFieldContext, schemaContext, argumentParam, contextChanged, replacer);
            }
            else
            {
                (nextFieldContext, selectionFields, _) = ProcessExtensionsSelection(nextFieldContext, selectionFields, null, argumentParam, contextChanged, replacer);
                // build a new {...} - returning a single object {}
                var newExp = ExpressionUtil.CreateNewExpression(Name, selectionFields.ExpressionOnly(), out Type anonType);
                if (nextFieldContext.NodeType != ExpressionType.MemberInit && nextFieldContext.NodeType != ExpressionType.New)
                {
                    // make a null check from this new expression
                    nextFieldContext = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, nextFieldContext, Expression.Constant(null)), Expression.Constant(null, anonType), newExp!, anonType);
                }
                else
                {
                    nextFieldContext = newExp;
                }
            }

            return nextFieldContext;
        }

        /// <summary>
        /// These expression will be built on the element type
        /// we might be using a service i.e. ctx => WithService((T r) => r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()))
        /// if we can we want to avoid calling that multiple times with a expression like
        /// r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()) == null ? null : new {
        ///      Field = r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()).Blah
        /// }
        /// by wrapping the whole thing in a method that does the null check once.
        /// This means we build the fieldExpressions on a parameter of the result type
        /// </summary>
        /// <param name="selectionFields">Fields to select once we know if this result is null or not</param>
        /// <param name="serviceProvider"></param>
        /// <param name="nextFieldContext">The expression that the selection fields will be built from</param>
        /// <param name="schemaContext"></param>
        /// <param name="contextChanged">Has the context changes (second pass with services)</param>
        /// <param name="replacer"></param>
        /// <returns></returns>
        private Expression WrapWithNullCheck(CompileContext compileContext, Dictionary<IFieldKey, CompiledField> selectionFields, IServiceProvider? serviceProvider, Expression nextFieldContext, ParameterExpression schemaContext, ParameterExpression? argumentParam, bool contextChanged, ParameterReplacer replacer)
        {
            // selectionFields is set up but we need to wrap
            // we wrap here as we have access to the values and services etc
            var fieldParamValues = new List<object?>(compileContext.ConstantParameters.Values);
            var fieldParams = new List<ParameterExpression>(compileContext.ConstantParameters.Keys);

            // do not need to inject serivces here as this expression is used in the call arguments
            var updatedExpression = nextFieldContext;
            // replace with null_wrap
            // this is the parameter used in the null wrap. We pass it to the wrap function which has the value to match
            var nullWrapParam = Expression.Parameter(updatedExpression.Type, "nullwrap");

            if (contextChanged)
            {
                foreach (var item in selectionFields)
                {
                    if (item.Value.Field.HasServices || item.Key.Name == "__typename")
                        item.Value.Expression = replacer.ReplaceByType(item.Value.Expression, nextFieldContext.Type, nullWrapParam);
                    else
                        item.Value.Expression = Expression.PropertyOrField(nullWrapParam, item.Key.Name);
                }
            }
            else
            {
                foreach (var item in selectionFields)
                {
                    item.Value.Expression = replacer.ReplaceByType(item.Value.Expression, nextFieldContext.Type, nullWrapParam);
                }
            }

            (updatedExpression, selectionFields, _) = ProcessExtensionsSelection(updatedExpression, selectionFields, null, argumentParam, contextChanged, replacer);
            // we need to make sure the wrap can resolve any services in the select
            var selectionExpressions = selectionFields.ToDictionary(f => f.Key.Name, f => GraphQLHelper.InjectServices(serviceProvider!, compileContext.Services, fieldParamValues, f.Value.Expression, fieldParams, replacer));

            updatedExpression = ExpressionUtil.WrapObjectProjectionFieldForNullCheck(Name, updatedExpression, fieldParams, selectionExpressions, fieldParamValues, nullWrapParam, schemaContext);
            return updatedExpression;
        }
    }
}
