using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLMutationStatement : ExecutableGraphQLStatement
    {
        private readonly ParameterReplacer replacer;

        public GraphQLMutationStatement(string name, IEnumerable<GraphQLMutationField> mutationFields)
        {
            Name = name;
            QueryFields = mutationFields.ToList();
            replacer = new ParameterReplacer();
        }

        public override async Task<ConcurrentDictionary<string, object>> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, bool executeServiceFieldsSeparately)
        {
            var result = new ConcurrentDictionary<string, object>();
            foreach (GraphQLMutationField node in QueryFields)
            {
                result[node.Name] = null;
                try
                {
                    var data = await ExecuteAsync(node, context, validator, serviceProvider, fragments, fieldNamer, executeServiceFieldsSeparately);

                    result[node.Name] = data;
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLCompilerException($"Error executing query field {node.Name}", ex);
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
        private async Task<object> ExecuteAsync<TContext>(GraphQLMutationField node, TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, bool executeServiceFieldsSeparately)
        {
            // run the mutation to get the context for the query select
            var result = await node.ExecuteMutationAsync(context, validator, serviceProvider, fieldNamer);

            if (result == null || // result is null and don't need to do anything more
                node.ResultSelection == null) // mutation must return a scalar type
                return result;

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

                ParameterExpression resultContextParam = node.ResultSelection.RootFieldParameter;
                var selectContext = resultContextParam.Type;
                string capMethod = null;

                if (!mutationLambda.ReturnType.IsEnumerableOrArray())
                {
                    if (mutationContextExpression.NodeType == ExpressionType.Call)
                    {
                        // In the case of a First() we need to insert that select before the first
                        // This is all to have 1 nice expression that can work with ORMs (like EF)
                        // E.g  we want db => db.Entity.Select(e => new {name = e.Name, ...}).First(filter)
                        // we dot not want db => new {name = db.Entity.First(filter).Name, ...})

                        var call = (MethodCallExpression)mutationContextExpression;
                        if (call.Method.Name == "First" || call.Method.Name == "FirstOrDefault" ||
                            call.Method.Name == "Last" || call.Method.Name == "LastOrDefault" ||
                            call.Method.Name == "Single" || call.Method.Name == "SingleOrDefault")
                        {
                            // Get the expression that we can add the Select() too
                            mutationContextExpression = call.Arguments.First();
                            if (call.Arguments.Count == 2)
                            {
                                // this is a ctx.Something.First(f => ...)
                                // move the filter to a Where call so we can use .Select() to get the fields requested
                                var filter = call.Arguments.ElementAt(1);
                                mutationContextExpression = ExpressionUtil.MakeCallOnQueryable("Where", new Type[] { resultContextParam.Type }, mutationContextExpression, filter);
                            }

                            capMethod = call.Method.Name;
                        }
                        else
                        {
                            throw new EntityGraphQLCompilerException($"Mutation return expression type not supported. {call}");
                        }
                    }
                    else
                    {
                        // if they just return a constant I.e the entity they just updated. It comes as a member access constant
                        if (mutationLambda.Body.NodeType == ExpressionType.MemberAccess)
                        {
                            var me = (MemberExpression)mutationLambda.Body;
                            if (me.Expression.NodeType == ExpressionType.Constant)
                            {
                                node.ResultSelection.ConstantParameters[Expression.Parameter(me.Type, $"const_{me.Type.Name}")] = Expression.Lambda(me).Compile().DynamicInvoke();
                            }
                        }
                        else if (mutationLambda.Body.NodeType == ExpressionType.Constant)
                        {
                            var ce = (ConstantExpression)mutationLambda.Body;
                            node.ResultSelection.ConstantParameters[Expression.Parameter(ce.Type, $"const_{ce.Type.Name}")] = ce.Value;
                        }
                    }
                }

                ExpressionResult selectExp = null;
                // We now have the expression to build the select on
                // first without service fields
                object dataContext = context;
                if (node.ResultSelection.HasAnyServices(fragments) && executeServiceFieldsSeparately)
                {
                    selectExp = node.ResultSelection.GetNodeExpression(serviceProvider, fragments, true);
                    if (capMethod != null && selectExp != null)
                    {
                        selectExp = ExpressionUtil.MakeCallOnQueryable("Select", new Type[] { selectContext, selectExp.Type }, mutationContextExpression, Expression.Lambda(selectExp, resultContextParam));

                        // materialize expression (could be EF/ORM) with our capMethod
                        selectExp = ExpressionUtil.MakeCallOnQueryable(capMethod, new Type[] { selectExp.Type.GetGenericArguments()[0] }, selectExp);

                        dataContext = ExecuteExpression(selectExp, context, mutationContextParam, serviceProvider, node.ResultSelection, replacer);
                        mutationContextParam = Expression.Parameter(dataContext.GetType(), "_ctx");

                        // Add Select with service fields
                        selectExp = node.ResultSelection.GetNodeExpression(serviceProvider, fragments, false, mutationContextParam, isMutationResult: true);
                    }
                }
                if (selectExp == null)
                {
                    selectExp = node.ResultSelection.GetNodeExpression(serviceProvider, fragments, false);
                    if (capMethod != null)
                    {
                        selectExp = ExpressionUtil.MakeCallOnQueryable("Select", new Type[] { selectContext, selectExp.Type }, mutationContextExpression, Expression.Lambda(selectExp, resultContextParam));

                        // materialize expression (could be EF/ORM) with our capMethod
                        selectExp = ExpressionUtil.MakeCallOnQueryable(capMethod, new Type[] { selectExp.Type.GetGenericArguments()[0] }, selectExp);
                    }
                }

                var data = ExecuteExpression(selectExp, dataContext, mutationContextParam, serviceProvider, node.ResultSelection, replacer);
                return data;
            }

            // run the query select against the object they have returned directly from the mutation
            result = CompileAndExecuteNode(result, serviceProvider, fragments, node.ResultSelection, executeServiceFieldsSeparately);
            return result;
        }
    }
}
