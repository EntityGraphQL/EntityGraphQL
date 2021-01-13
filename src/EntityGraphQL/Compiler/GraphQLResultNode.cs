using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

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
        /// A list of graphql operations. These could be mutations or queries
        /// </summary>
        /// <value></value>
        public List<GraphQLQueryNode> Operations { get; }
        public ParameterExpression RootFieldParameter => throw new NotImplementedException();
        public IEnumerable<Type> Services => throw new NotImplementedException();
        public bool HasAnyServices { get; } = false;

        public GraphQLResultNode()
        {
            this.Operations = new List<GraphQLQueryNode>();
        }

        public string Name
        {
            get => "Query Request Root";
            set => throw new NotImplementedException();
        }

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters => throw new NotImplementedException();

        public QueryResult ExecuteQuery<TContext>(TContext context, IServiceProvider services, string operationName = null)
        {
            return ExecuteQueryAsync(context, services, operationName).Result;
        }

        /// <summary>
        /// Executes the compiled GraphQL document adding data results into QueryResult.
        /// If no OperationName is supplied the first operation in the query document is executed
        /// </summary>
        /// <param name="context">Instance of the context tyoe of the schema</param>
        /// <param name="services">Service provider used for DI</param>
        /// <param name="operationName">Optional operation name</param>
        /// <returns></returns>
        public async Task<QueryResult> ExecuteQueryAsync<TContext>(TContext context, IServiceProvider services, string operationName = null)
        {
            // check operation names
            if (Operations.Count > 1 && Operations.Count(o => string.IsNullOrEmpty(o.Name)) > 0)
            {
                throw new EntityGraphQLCompilerException("An operation name must be defined for all operations if there are multiple operations in the request");
            }
            var result = new QueryResult();
            var validator = new GraphQLValidator();
            var op = string.IsNullOrEmpty(operationName) ? Operations.First() : Operations.First(o => o.Name == operationName);
            // execute all root level nodes in the op
            // e.g. op = query Op1 {
            //      people { name id }
            //      movies { released name }
            // }
            // people & movies will be the 2 fields that will be executed
            foreach (var node in op.QueryFields)
            {
                result.Data[node.Name] = null;
                // request.Variables are already compiled into the expression
                var data = await ((GraphQLExecutableNode)node).ExecuteAsync(context, validator, services);
                result.Data[node.Name] = data;
            }

            if (validator.Errors.Count > 0)
                result.AddErrors(validator.Errors);

            return result;
        }

        public ExpressionResult GetNodeExpression(object contextValue, IServiceProvider serviceProvider, bool withoutServiceFields = false, ParameterExpression buildServiceWrapWithType = null)
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

        public IEnumerable<IGraphQLBaseNode> GetFieldsWithoutServices(ParameterExpression contextParam)
        {
            return new List<IGraphQLBaseNode>();
        }
        public ParameterExpression FindRootParameterExpression()
        {
            throw new NotImplementedException();
        }
    }
}