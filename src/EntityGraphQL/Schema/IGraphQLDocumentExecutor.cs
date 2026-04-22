using System;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema;

/// <summary>
/// Executes a parsed GraphQL document against a context instance.
/// This is the stage-2 execution boundary after parsing/validation and before data is returned.
/// </summary>
public interface IGraphQLDocumentExecutor
{
    Task<QueryResult> ExecuteAsync<TContext>(
        GraphQLDocument document,
        TContext? context,
        IServiceProvider? serviceProvider,
        QueryVariables? variables,
        string? operationName,
        QueryRequestContext requestContext,
        ExecutionOptions options,
        CancellationToken cancellationToken
    );
}
