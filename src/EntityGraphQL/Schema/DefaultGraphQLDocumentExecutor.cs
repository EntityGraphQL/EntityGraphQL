using System;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema;

/// <summary>
/// Default document executor that uses EntityGraphQL's expression-tree execution pipeline.
/// </summary>
public class DefaultGraphQLDocumentExecutor : IGraphQLDocumentExecutor
{
    public Task<QueryResult> ExecuteAsync<TContext>(
        GraphQLDocument document,
        TContext? context,
        IServiceProvider? serviceProvider,
        QueryVariables? variables,
        string? operationName,
        QueryRequestContext requestContext,
        ExecutionOptions options,
        CancellationToken cancellationToken
    )
    {
        return document.ExecuteQueryAsync(context, serviceProvider, variables, operationName, requestContext, options, cancellationToken);
    }
}
