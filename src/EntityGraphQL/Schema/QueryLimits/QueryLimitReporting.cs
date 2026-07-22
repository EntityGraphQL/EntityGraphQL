namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// How exceeded query limits (<see cref="ExecutionOptions.MaxQueryDepth"/>, <see cref="ExecutionOptions.MaxFieldSelections"/>,
/// <see cref="ExecutionOptions.MaxFieldAliases"/>, <see cref="ExecutionOptions.MaxQueryComplexity"/>) are handled.
/// </summary>
public enum QueryLimitsMode
{
    /// <summary>
    /// The query is rejected with a document error before any execution. The default.
    /// </summary>
    Enforce,

    /// <summary>
    /// Exceeded limits are reported via <see cref="ExecutionOptions.OnQueryLimitExceeded"/> but the query still
    /// executes. Use to observe what real clients do before enforcing - deploy report-only, watch the logs,
    /// then switch to <see cref="Enforce"/> with confident numbers.
    /// </summary>
    ReportOnly,
}

/// <summary>
/// The query limit that was exceeded.
/// </summary>
public enum QueryLimitKind
{
    /// <summary><see cref="ExecutionOptions.MaxQueryDepth"/></summary>
    QueryDepth,

    /// <summary><see cref="ExecutionOptions.MaxFieldSelections"/></summary>
    FieldSelections,

    /// <summary><see cref="ExecutionOptions.MaxFieldAliases"/></summary>
    FieldAliases,

    /// <summary><see cref="ExecutionOptions.MaxQueryComplexity"/></summary>
    QueryComplexity,
}

/// <summary>
/// Receives exceeded query limits. Register in the service provider as an alternative to setting
/// <see cref="ExecutionOptions.OnQueryLimitExceeded"/> - useful when the observer wants injected
/// dependencies (a logger, the current user via IHttpContextAccessor, a metrics sink). When both are
/// present the <see cref="ExecutionOptions.OnQueryLimitExceeded"/> callback wins, mirroring
/// <see cref="ExecutionOptions.FieldRateLimitService"/> vs the service-provider registration.
/// </summary>
public interface IQueryLimitObserver
{
    /// <summary>
    /// Called for each exceeded query limit. Keep implementations fast and non-throwing; an exception
    /// thrown here fails the request.
    /// </summary>
    void OnQueryLimitExceeded(QueryLimitExceededContext context);
}

/// <summary>
/// Details of an exceeded query limit, passed to <see cref="ExecutionOptions.OnQueryLimitExceeded"/>.
/// Grouped in a struct so the callback signature can evolve without breaking implementations.
/// </summary>
public readonly struct QueryLimitExceededContext
{
    public QueryLimitExceededContext(QueryLimitKind limit, int actual, int maximum, string? operationName)
    {
        Limit = limit;
        Actual = actual;
        Maximum = maximum;
        OperationName = operationName;
    }

    /// <summary>Which limit was exceeded.</summary>
    public QueryLimitKind Limit { get; }

    /// <summary>
    /// The observed value. In <see cref="QueryLimitsMode.ReportOnly"/> this is the full value for the document
    /// (e.g. the query's actual depth). In <see cref="QueryLimitsMode.Enforce"/> validation stops at the first
    /// violation, so this is the value at the point the limit tripped.
    /// </summary>
    public int Actual { get; }

    /// <summary>The configured limit.</summary>
    public int Maximum { get; }

    /// <summary>The GraphQL operation name, if the operation is named.</summary>
    public string? OperationName { get; }
}
