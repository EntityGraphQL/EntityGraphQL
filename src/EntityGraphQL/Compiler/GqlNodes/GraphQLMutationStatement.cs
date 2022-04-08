using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLMutationStatement : ExecutableGraphQLStatement
    {
        public GraphQLMutationStatement(string name, Expression nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(name, nodeExpression, rootParameter, parentNode)
        {
            Name = name;
        }

        public override async Task<ConcurrentDictionary<string, object?>> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, ExecutionOptions options)
        {
            var result = new ConcurrentDictionary<string, object?>();
            foreach (GraphQLMutationField node in QueryFields)
            {
                result[node.Name] = null;
                try
                {
                    var data = await ExecuteAsync(node, context, validator, serviceProvider, fragments, fieldNamer, options);

                    result[node.Name] = data;
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLExecutionException(node.Name, ex);
                }
            }
            return result;
        }

        /// <summary>
        /// Execute the current mutation
        /// </summary>
        /// <param name="context">The context instance that will be used</param>
        /// <param name="serviceProvider">A service provider to look up any dependencies</param>
        /// <typeparam name="TContext"></typeparam>
        /// <returns></returns>
        private async Task<object?> ExecuteAsync<TContext>(GraphQLMutationField node, TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, ExecutionOptions options)
        {
            if (context == null)
                return null;
            // run the mutation to get the context for the query select
            var result = await node.ExecuteMutationAsync(context, validator, serviceProvider, fieldNamer);

            if (result == null || // result is null and don't need to do anything more
                node.ResultSelection == null) // mutation must return a scalar type
                return result;

            var resultExp = node.ResultSelection;

            if (typeof(LambdaExpression).IsAssignableFrom(result.GetType()))
            {
                // If they have returned an Expression, we need to join the select Expression with the
                // expression they returned which gives us the full Expression executable over the whole
                // GraphQL schema

                // this will typically be similar to
                // db => db.Entity.Where(filter) or db => db.Entity.First(filter)
                // i.e. they'll be returning a list of items or a specific item
                // We want to take the field selection from the GraphQL query and add a LINQ Select() onto the expression

                var mutationLambda = (LambdaExpression)result;
                var mutationContextParam = mutationLambda.Parameters.First();

                var mutationContextExpression = mutationLambda.Body;

                if (!mutationContextExpression.Type.IsEnumerableOrArray())
                {
                    var listExp = ExpressionUtil.FindEnumerable(mutationContextExpression);
                    if (listExp.Item1 != null && mutationContextExpression.NodeType == ExpressionType.Call)
                    {
                        // yes we can
                        // rebuild the Expression so we keep any ConstantParameters
                        var item1 = listExp.Item1;
                        var collectionNode = new GraphQLListSelectionField(null, null, Name, node.ResultSelection.RootParameter, node.ResultSelection.RootParameter, item1, node, null);
                        foreach (var queryField in node.ResultSelection.QueryFields)
                        {
                            collectionNode.AddField(queryField);
                        }
                        var newNode = new GraphQLCollectionToSingleField(collectionNode, (GraphQLObjectProjectionField)resultExp, listExp.Item2!);
                        resultExp = newNode;
                    }
                    else
                    {
                        SetupConstants(resultExp, mutationContextExpression);
                    }
                }
                else
                {
                    // we now know the context as it is dynamically returned in a mutation
                    if (resultExp is GraphQLListSelectionField listField)
                    {
                        listField.ListExpression = mutationContextExpression;
                    }
                    SetupConstants(resultExp, mutationContextExpression);
                }
                resultExp.RootParameter = mutationContextParam;

                result = CompileAndExecuteNode(context, serviceProvider, fragments, resultExp, options);
                return result;
            }
            // we now know the context as it is dynamically returned in a mutation
            if (resultExp is GraphQLListSelectionField field)
            {
                var contextParam = Expression.Parameter(result.GetType());
                resultExp.RootParameter = contextParam;
                field.ListExpression = contextParam;
            }

            // run the query select against the object they have returned directly from the mutation
            result = CompileAndExecuteNode(result, serviceProvider, fragments, node.ResultSelection, options);
            return result;
        }

        private static void SetupConstants(BaseGraphQLQueryField resultExp, Expression mutationContextExpression)
        {
            // if they just return a constant I.e the entity they just updated. It comes as a member access constant
            if (mutationContextExpression.NodeType == ExpressionType.MemberAccess)
            {
                var me = (MemberExpression)mutationContextExpression;
                if (me.Expression.NodeType == ExpressionType.Constant)
                {
                    resultExp.ConstantParameters[Expression.Parameter(me.Type, $"const_{me.Type.Name}")] = Expression.Lambda(me).Compile().DynamicInvoke();
                }
            }
            else if (mutationContextExpression.NodeType == ExpressionType.Constant)
            {
                var ce = (ConstantExpression)mutationContextExpression;
                resultExp.ConstantParameters[Expression.Parameter(ce.Type, $"const_{ce.Type.Name}")] = ce.Value;
            }
        }
    }
}
