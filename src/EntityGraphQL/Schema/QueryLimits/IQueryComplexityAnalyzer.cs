using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Analyzes the complexity of a parsed GraphQL query. Used by the query-limit validator to reject
/// queries whose cost exceeds <see cref="ExecutionOptions.MaxQueryComplexity"/>.
/// Implementations must be thread-safe.
/// </summary>
public interface IQueryComplexityAnalyzer
{
    /// <summary>
    /// Calculate the complexity of the given compiled query document for the named operation.
    /// </summary>
    /// <param name="document">The parsed GraphQL document (produced by EntityGraphQL's compiler).</param>
    /// <param name="operationName">Name of the operation to analyze, or null for the first operation.</param>
    /// <param name="variables">Query variables from the request, used to resolve <c>$var</c> references in field arguments.</param>
    /// <param name="options">Execution options with limits and tuning knobs (list multiplier cap, etc.).</param>
    int CalculateComplexity(GraphQLDocument document, string? operationName, QueryVariables? variables, ExecutionOptions options);
}
