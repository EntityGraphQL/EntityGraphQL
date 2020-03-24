using System;
using System.Collections.Generic;
using System.Linq;

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
        /// A list of graphql operations. THese could be mutations or queries
        /// </summary>
        /// <value></value>
        public List<IGraphQLNode> Operations { get; }
        public OperationType Type => OperationType.Result;

        public GraphQLResultNode(IEnumerable<IGraphQLNode> operations)
        {
            this.Operations = operations.ToList();
        }

        public string Name => "Query Request Root";

        /// <summary>
        /// Executes the compiled GraphQL document adding data results into QueryResult.
        /// If no OperationName is supplied the first operation in the query document is executed
        /// </summary>
        /// <param name="context">Instance of the context tyoe of the schema</param>
        /// <param name="services">Service provider used for DI</param>
        /// <param name="operationName">Optional operation name</param>
        /// <returns></returns>
        public QueryResult ExecuteQuery<TContext>(TContext context, IServiceProvider services, string operationName = null)
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
                var data = node.Execute(context, services);
                result.Data[node.Name] = data;
            }

            return result;
        }
    }
}