using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Top level result of parsing a GraphQL document.
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
    public class GraphQLDocument : IGraphQLNode
    {
        public Expression? NextFieldContext { get; }
        public IGraphQLNode? ParentNode { get; }
        public ParameterExpression? RootParameter { get; }
        public List<BaseGraphQLField> QueryFields { get; } = new List<BaseGraphQLField>();

        /// <summary>
        /// A list of GraphQL operations. These could be mutations or queries
        /// </summary>
        /// <value></value>
        public List<ExecutableGraphQLStatement> Operations { get; }
        public List<GraphQLFragmentStatement> Fragments { get; set; }
        public GraphQLDocument(ISchemaProvider schema)
        {
            Schema = schema;
            Operations = new List<ExecutableGraphQLStatement>();
            Fragments = new List<GraphQLFragmentStatement>();
            Arguments = new Dictionary<string, object>();
        }

        public string Name
        {
            get => "Query Request Root";
        }

        public IField? Field { get; }
        public bool HasServices { get => Field?.Services.Any() == true; }

        public IReadOnlyDictionary<string, object> Arguments { get; }

        public ISchemaProvider Schema { get; }

        public QueryResult ExecuteQuery<TContext>(TContext context, IServiceProvider? services, QueryVariables? variables, string? operationName = null, ExecutionOptions? options = null)
        {
            return ExecuteQueryAsync(context, services, variables, operationName, options).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes the compiled GraphQL document adding data results into QueryResult.
        /// If no OperationName is supplied the first operation in the query document is executed
        /// </summary>
        /// <param name="overwriteContext">Instance of the context type of the schema. If provided this is used whereever the context is needed.
        /// If not supplied the context is fetched from serviceProvider allowing you to control the scope</param>
        /// <param name="serviceProvider">Service provider used for DI</param>
        /// <param name="variables">Variables passed in the request</param>
        /// <param name="operationName">Optional name of operation to execute. If null the first operation will be executed</param>
        /// <param name="options">Options for execution.</param>
        /// <returns></returns>
        public async Task<QueryResult> ExecuteQueryAsync<TContext>(TContext? overwriteContext, IServiceProvider? serviceProvider, QueryVariables? variables, string? operationName, ExecutionOptions? options = null)
        {
            // check operation names
            if (Operations.Count > 1 && Operations.Any(o => string.IsNullOrEmpty(o.Name)))
                throw new EntityGraphQLExecutionException("An operation name must be defined for all operations if there are multiple operations in the request");

            var result = new QueryResult();
            IGraphQLValidator? validator = serviceProvider?.GetService<IGraphQLValidator>();
            var op = string.IsNullOrEmpty(operationName) ? Operations.First() : Operations.First(o => o.Name == operationName);

            // execute the selected operation
            options ??= new ExecutionOptions(); // defaults

            result.SetData(await op.ExecuteAsync(overwriteContext, serviceProvider, Fragments, Schema.SchemaFieldNamer, options, variables));

            if (validator?.Errors.Count > 0)
                result.AddErrors(validator.Errors);

            return result;
        }

        public void AddField(BaseGraphQLField field)
        {
            throw new NotImplementedException();
        }

        public void AddDirectives(IEnumerable<GraphQLDirective> graphQLDirectives)
        {
            throw new NotImplementedException();
        }
    }
}