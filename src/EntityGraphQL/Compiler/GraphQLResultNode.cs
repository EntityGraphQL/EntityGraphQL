using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLResultNode : IGraphQLBaseNode
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

        /// <summary>
        /// Executes the compiled GraphQL document adding data results into QueryResult
        /// </summary>
        /// <param name="context">The context object to apply the compiled Lambda to. E.g. a DbContext</param>
        /// <param name="operationName">Optional, the operation name to execute from in the query. If null or empty the first operation is executed</param>
        /// <returns></returns>
        public QueryResult ExecuteQuery(object context, string operationName = null)
        {
            var result = new QueryResult();
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