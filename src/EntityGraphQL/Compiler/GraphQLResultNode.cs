using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Top level result of executing a GraphQL request.
    /// Contains a list of top level operations defined in the query document. They can either be queries or mutations.
    /// Also contains a list of fragments defined in the query document
    /// e.g.
    /// {
    ///     query Op1 {
    ///         people { name id }
    ///         movies { name released }
    ///     }
    ///     query Op2 {
    ///         ...
    ///     }
    ///     mutation ...
    ///     fragment ...
    /// }
    /// </summary>
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
        public OperationType Type => OperationType.Result;

        public GraphQLResultNode(IEnumerable<IGraphQLNode> operations, List<GraphQLFragment> fragments)
        {
            this.Operations = operations.ToList();
            this.fragments = fragments;
        }

        public string Name => "Query Request Root";

        /// <summary>
        /// Executes the compiled GraphQL document adding data results into QueryResult.
        /// If no OperationName is supplied the first operation in the query document is executed
        /// </summary>
        /// <param name="context">The context object to apply the compiled Lambda to. E.g. a DbContext</param>
        /// <param name="operationName">Optional, the operation name to execute from in the query. If null or empty the first operation is executed</param>
        /// <returns></returns>
        public QueryResult ExecuteQuery(object context, string operationName = null, params object[] mutationArgs)
        {
            var result = new QueryResult();
            var op = string.IsNullOrEmpty(operationName) ? Operations.First() : Operations.First(o => o.Name == operationName);
            // execute all root level nodes in the op
            // e.g. op = query Op1 {
            //      people { name id }
            //      movies { released name }
            // }
            // people & movies will be the 2 fields that will be executed
            foreach (var node in op.Fields)
            {
                result.Data[node.Name] = null;
                // request.Variables are already compiled into the expression
                var args = new List<object> {context};
                if (node.Type == OperationType.Mutation)
                {
                    args.AddRange(mutationArgs);
                }
                var data = node.Execute(args.ToArray());
                result.Data[node.Name] = data;
            }

            return result;
        }
    }
}