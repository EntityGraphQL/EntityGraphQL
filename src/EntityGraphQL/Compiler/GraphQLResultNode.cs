using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLResultNode : IGraphQLNode
    {
        /// <summary>
        /// A list of the fragments in thw query document
        /// </summary>
        private List<GraphQLFragment> fragments;
        /// <summary>
        /// A list of graphql operations. THese could be mutations or queries
        /// </summary>
        /// <value></value>
        public List<IGraphQLNode> Operations { get; }

        public GraphQLResultNode(IEnumerable<IGraphQLNode> operations, List<GraphQLFragment> fragments)
        {
            this.Operations = operations.ToList();
            this.fragments = fragments;
        }

        public string Name => "Query Request Root";

        public List<IGraphQLNode> Fields => throw new NotImplementedException();

        public List<object> ConstantParameterValues => throw new NotImplementedException();

        public List<ParameterExpression> Parameters => throw new NotImplementedException();

        public ExpressionResult NodeExpression { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Expects 3 arguments and just calls ExecuteQuery().
        /// </summary>
        /// <param name="args">Must be 3. The context, a QueryResult to add results to and an operationName (can be null)</param>
        /// <returns></returns>
        public object Execute(params object[] args)
        {
            if (args.Length != 3)
                throw new ArgumentException("Must supply 2 arguments. The context and a QueryResult instance");
            var context = args[0];
            var result = (QueryResult)args[1];
            var operationName = (string)args[2];
            return ExecuteQuery(context, result, operationName);
         }

        /// <summary>
        /// Executes the compiled GraphQL document adding data results into QueryResult
        /// </summary>
        /// <param name="context">The context object to apply the compiled Lambda to. E.g. a DbContext</param>
        /// <param name="result">A QueryResult instance in which data results or errors will be added to</param>
        /// <returns></returns>
        public object ExecuteQuery(object context, QueryResult result, string operationName)
        {
            var op = string.IsNullOrEmpty(operationName) ? Operations.First() : Operations.First(o => o.Name == operationName);
            foreach (var node in op.Fields)
            {
                result.Data[node.Name] = null;
                // request.Variables are already compiled into the expression
                var data = node.Execute(context);
                result.Data[node.Name] = data;
            }
            return result;
        }
    }
}