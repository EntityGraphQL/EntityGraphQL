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
