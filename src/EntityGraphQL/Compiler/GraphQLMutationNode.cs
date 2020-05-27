using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLMutationNode : GraphQLExecutableNode, IGraphQLBaseNode
    {
        private readonly MutationType mutationType;
        private readonly Dictionary<string, ExpressionResult> args;
        private readonly GraphQLQueryNode resultSelection;

        public string Name { get => resultSelection.Name; set => throw new NotImplementedException(); }

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters => resultSelection.ConstantParameters;

        public GraphQLMutationNode(MutationType mutationType, Dictionary<string, ExpressionResult> args, GraphQLQueryNode resultSelection)
        {
            this.mutationType = mutationType;
            this.args = args;
            this.resultSelection = resultSelection;
        }

        public ExpressionResult GetNodeExpression()
        {
            throw new NotImplementedException();
        }

        private async Task<object> ExecuteMutationAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider)
        {
            try
            {
                return await mutationType.CallAsync(context, args, validator, serviceProvider);
            }
            catch (EntityQuerySchemaException e)
            {
                throw new EntityQuerySchemaException($"Error applying mutation: {e.Message}");
            }
        }

        /// <summary>
        /// Execute the current mutation
        /// </summary>
        /// <param name="context">The context instance that will be used</param>
        /// <param name="serviceProvider">A service provider to look up any dependencies</param>
        /// <typeparam name="TContext"></typeparam>
        /// <returns></returns>
        public override object Execute<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider)
        {
            // run the mutation to get the context for the query select
            var result = ExecuteMutationAsync(context, validator, serviceProvider).Result;
            if (result == null)
                return null;
            if (typeof(LambdaExpression).IsAssignableFrom(result.GetType()))
            {
                var mutationLambda = (LambdaExpression)result;
                var mutationContextParam = mutationLambda.Parameters.First();
                var mutationExpression = mutationLambda.Body;

                // this will typically be similar to
                // db => db.Entity.Where(filter) or db => db.Entity.First(filter)
                // i.e. they'll be returning a list of items or a specific item
                // We want to take the field selection from the GraphQL query and add a LINQ Select() onto the expression
                // In the case of a First() we need to insert that select before the first
                // This is all to have 1 nice expression that can work with ORMs (like EF)
                // E.g  we want db => db.Entity.Select(e => new {name = e.Name, ...}).First(filter)
                // we dot not want db => new {name = db.Entity.First(filter).Name, ...})

                var selectParam = resultSelection.FieldParameter;

                if (!mutationLambda.ReturnType.IsEnumerableOrArray())
                {
                    if (mutationExpression.NodeType == ExpressionType.Call)
                    {
                        var call = (MethodCallExpression)mutationExpression;
                        if (call.Method.Name == "First" || call.Method.Name == "FirstOrDefault" || call.Method.Name == "Last" || call.Method.Name == "LastOrDefault")
                        {
                            var baseExp = call.Arguments.First();
                            if (call.Arguments.Count == 2)
                            {
                                // this is a ctx.Something.First(f => ...)
                                // move the fitler to a Where call
                                var filter = call.Arguments.ElementAt(1);
                                baseExp = ExpressionUtil.MakeCallOnQueryable("Where", new Type[] { selectParam.Type }, baseExp, filter);
                            }

                            // build select
                            var selectExp = ExpressionUtil.MakeCallOnQueryable("Select", new Type[] { selectParam.Type, resultSelection.GetNodeExpression().Type }, baseExp, Expression.Lambda(resultSelection.GetNodeExpression(), selectParam));

                            // add First/Last back
                            var firstExp = ExpressionUtil.MakeCallOnQueryable(call.Method.Name, new Type[] { selectExp.Type.GetGenericArguments()[0] }, selectExp);

                            // we're done
                            resultSelection.SetNodeExpression(firstExp);
                        }
                    }
                    else
                    {
                        // if they just return a constant I.e the entity they just updated. It comes as a memebr access constant
                        if (mutationLambda.Body.NodeType == ExpressionType.MemberAccess)
                        {
                            var me = (MemberExpression)mutationLambda.Body;
                            if (me.Expression.NodeType == ExpressionType.Constant)
                            {
                                resultSelection.AddConstantParameter(Expression.Parameter(me.Type, $"const_{me.Type.Name}"), Expression.Lambda(me).Compile().DynamicInvoke());
                            }
                        }
                        else if (mutationLambda.Body.NodeType == ExpressionType.Constant)
                        {
                            var ce = (ConstantExpression)mutationLambda.Body;
                            resultSelection.AddConstantParameter(Expression.Parameter(ce.Type, $"const_{ce.Type.Name}"), ce.Value);
                        }
                    }
                }
                else
                {
                    var exp = ExpressionUtil.MakeCallOnQueryable("Select", new Type[] { selectParam.Type, resultSelection.GetNodeExpression().Type }, mutationExpression, Expression.Lambda(resultSelection.GetNodeExpression(), selectParam));
                    resultSelection.SetNodeExpression(exp);
                }

                // make sure we use the right parameter
                resultSelection.FieldParameter = mutationContextParam;
                result = resultSelection.Execute(context, validator, serviceProvider);
                return result;
            }
            // run the query select
            result = resultSelection.Execute(result, validator, serviceProvider);
            return result;
        }

        public void AddConstantParameter(ParameterExpression param, object val)
        {
            throw new NotImplementedException();
        }

        public void SetNodeExpression(ExpressionResult expressionResult)
        {
            throw new NotImplementedException();
        }

        public void SetCombineExpression(Expression item2)
        {
            throw new NotImplementedException();
        }
    }
}
