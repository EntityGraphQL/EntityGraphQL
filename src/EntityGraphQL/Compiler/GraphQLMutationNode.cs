using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLMutationNode : IGraphQLNode
    {
        private readonly CompiledQueryResult result;
        private readonly IGraphQLNode graphQLNode;

        public IEnumerable<IGraphQLNode> Fields { get; private set; }

        public string Name => graphQLNode.Name;
        public OperationType Type => OperationType.Mutation;

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters => new Dictionary<ParameterExpression, object>();

        public List<ParameterExpression> Parameters => throw new NotImplementedException();

        public GraphQLMutationNode(CompiledQueryResult result, IGraphQLNode graphQLNode)
        {
            this.result = result;
            this.graphQLNode = graphQLNode;
            Fields = new List<IGraphQLNode>();
        }

        public ExpressionResult GetNodeExpression()
        {
            throw new NotImplementedException();
        }
        public void SetNodeExpression(ExpressionResult expr)
        {
            throw new NotImplementedException();
        }

        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);

            // run the mutation to get the context for the query select
            var mutation = (MutationResult)this.result.ExpressionResult;
            var result = mutation.Execute(args);
            if (result.GetType().GetTypeInfo().BaseType.GetTypeInfo().BaseType == typeof(LambdaExpression))
            {
                var mutationLambda = (LambdaExpression)result;
                var mutationContextParam = mutationLambda.Parameters.First();
                var mutationExpression = mutationLambda.Body;

                // this willtypically be similar to
                // db => db.Entity.Where(filter) or db => db.Entity.First(filter)
                // i.e. they'll be returning a list of items or a specific item
                // We want to take the field selection from the GraphQL query and add a LINQ Select() onto the expression
                // In the case of a First() we need to insert that select before the first
                // This is all to have 1 nice expression that can work with ORMs (like EF)
                // E.g  we want db => db.Entity.Select(e => new {name = e.Name, ...}).First(filter)
                // we dot not want db => new {name = db.Entity.First(filter).Name, ...})

                var selectParam = graphQLNode.Parameters.First();

                if (!mutationLambda.ReturnType.IsEnumerableOrArray() && mutationExpression.NodeType == ExpressionType.Call)
                {
                    var call = (MethodCallExpression)mutationExpression;
                    if (call.Method.Name == "First" || call.Method.Name == "FirstOrDefault" || call.Method.Name == "Last" || call.Method.Name == "LastOrDefault")
                    {
                        var baseExp = call.Arguments.First();
                        if (call.Arguments.Count == 2)
                        {
                            // move the fitler to a Where call
                            var filter = call.Arguments.ElementAt(1);
                            baseExp = Expression.Call(typeof(Queryable), "Where", new Type[] { selectParam.Type }, call.Arguments.First(), filter);
                        }

                        // build select
                        var selectExp = Expression.Call(typeof(Queryable), "Select", new Type[] { selectParam.Type, graphQLNode.GetNodeExpression().Type}, baseExp, Expression.Lambda(graphQLNode.GetNodeExpression(), selectParam));

                        // add First/Last back
                        var firstExp = Expression.Call(typeof(Queryable), call.Method.Name, new Type[] { selectExp.Type.GetGenericArguments()[0] }, selectExp);

                        // we're done
                        graphQLNode.SetNodeExpression((ExpressionResult)firstExp);
                    }
                    else
                    {
                        throw new QueryException($"Mutation {Name} has invalid return type of {result.GetType()}. Please return Expression<Func<TConext, TEntity>> or Expression<Func<TConext, IEnumerable<TEntity>>>");
                    }
                }
                else
                {
                    var exp = Expression.Call(typeof(Queryable), "Select", new Type[] { selectParam.Type, graphQLNode.GetNodeExpression().Type}, mutationExpression, Expression.Lambda(graphQLNode.GetNodeExpression(), selectParam));
                    graphQLNode.SetNodeExpression((ExpressionResult)exp);
                }

                // make sure we use the right parameter
                graphQLNode.Parameters[0] = mutationContextParam;
                result = graphQLNode.Execute(args[0]);
                return result;
            }
            // run the query select
            result = graphQLNode.Execute(result);
            return result;
        }
    }
}
