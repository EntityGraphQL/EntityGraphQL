using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Security.Claims;
using EntityGraphQL.Schema.QueryLimits;

namespace EntityGraphQL.Schema;

public class ExecutionOptions
{
    /// <summary>
    /// Turn on or off the pre selection of fields with no services.
    /// When enabled, EntityGraphQL will build an Expression that selects the whole object graph without service
    /// dependant fields (including any core context fields a service field requires). Then once executed it will
    /// build and execute a second Expression tree that will include the service fields.
    ///
    /// This allows the use of a ORM like EF where the first pass will all be translated into SQL by the ORM. Then in memory
    /// the second execution can use that data to build the final result uses the dependant services.
    /// </summary>
    /// <value></value>
    public bool ExecuteServiceFieldsSeparately { get; set; } = true;

    /// <summary>
    /// Enable support for persisted queries - https://www.apollographql.com/docs/react/api/link/persisted-queries/
    /// This will set EnableQueryCache = true as the cache is used to implement persisted queries.
    /// </summary>
    public bool EnablePersistedQueries { get; set; } = true;

    /// <summary>
    /// Enables a cache of recently compiled queries to speed up execution of highly used queries.
    /// Cache is used for persisted queries as well.
    /// Query results are not cached, only the compiled expression from the GraphQL document.
    /// </summary>
    public bool EnableQueryCache { get; set; } = true;

    /// <summary>
    /// Allows you to hook into just before an expression is executed and modify it to suit. Note that if
    /// <code>ExecuteServiceFieldsSeparately</code> is true, this will be called twice if your query includes fields with services.
    /// Second parameter bool isFinal == true if the expression is the final execution - this means
    ///  - ExecuteServiceFieldsSeparately = false, or
    ///  - The query does not reference any fields with services
    ///  - The query references fields with service and the first execution has completed (isFinal == false) and we are executing again to merge the service results
    /// </summary>
    public Func<Expression, bool, Expression>? BeforeExecuting { get; set; }

    /// <summary>
    /// Allows you to hook into just before the expression is built and modify it to suit. This is only called for a field on the Query
    /// type. If <code>ExecuteServiceFieldsSeparately</code> is true, this will only be called for the first execution (without services).
    ///
    /// First parameter is the expression of the root Query field that is being queried.
    /// Second parameter is the operation name being executed. This can be null
    /// Third parameter is the field name being executed.
    /// Function must return an expression that returns the same type as the input expression.
    /// </summary>
    public Func<Expression, string?, string, Expression>? BeforeRootFieldExpressionBuild { get; set; }

    /// <summary>
    /// Include information on the executed operation in the result extensions. This includes:
    /// - operation type (query, mutation, subscription)
    /// - types queried and fields selected on each type
    /// Useful for debugging or audit logs.
    /// </summary>
    public bool IncludeQueryInfo { get; set; }

    /// <summary>
    /// Service-level concurrency limits. Key is the service type, value is the max concurrent operations.
    /// This provides centralized control over concurrency for all fields using specific services.
    /// Useful to help rate limiting or throttling.
    /// Example: { [typeof(TmdbService)] = 5, [typeof(DatabaseService)] = 10 }
    /// </summary>
    public Dictionary<Type, int> ServiceConcurrencyLimits { get; set; } = [];

    /// <summary>
    /// Global query-level concurrency limit. If set, no more than this many async operations
    /// will run concurrently across the entire query execution, regardless of service type.
    /// This overrides any individual field or service limits.
    /// </summary>
    public int? MaxQueryConcurrency { get; set; }

    /// <summary>
    /// Maximum nesting depth of a GraphQL query. Fragment spreads and inline fragments do not add depth.
    /// Null or 0 means unlimited. Recommended production value: 10-15.
    /// Protects against deeply-nested query DoS attacks.
    /// </summary>
    public int? MaxQueryDepth { get; set; }

    /// <summary>
    /// Maximum number of field selections in a GraphQL query, counted after fragment/inline fragment expansion.
    /// Null or 0 means unlimited. Recommended production value: a few hundred to a few thousand depending on schema.
    /// Protects against batched-alias and fragment-spread DoS attacks where the same field is selected many times.
    /// </summary>
    public int? MaxQueryNodes { get; set; }

    /// <summary>
    /// Maximum number of aliased field selections in a GraphQL query. An alias is any selection whose response name
    /// differs from the schema field name (e.g. <c>foo: bar</c>).
    /// Null or 0 means unlimited. Recommended production value: 20-50.
    /// Protects against alias-batching DoS where attackers submit <c>{ a: field b: field c: field ... }</c>.
    /// </summary>
    public int? MaxFieldAliases { get; set; }

    /// <summary>
    /// Maximum allowed query complexity score. When set, a <see cref="IQueryComplexityAnalyzer"/> walks the
    /// parsed query tree and rejects queries whose cost exceeds this limit.
    /// Null or 0 means unlimited.
    ///
    /// Field cost defaults to 1. For fields whose cost depends on arguments (e.g. <c>first: 100</c>), use
    /// <c>field.SetComplexity(ctx =&gt; ...)</c> which receives the field's arguments and its children's cost.
    /// </summary>
    public int? MaxQueryComplexity { get; set; }

    /// <summary>
    /// Optional custom complexity analyzer. If null and <see cref="MaxQueryComplexity"/> is set,
    /// a <see cref="DefaultQueryComplexityAnalyzer"/> will be used.
    /// </summary>
    public IQueryComplexityAnalyzer? QueryComplexityAnalyzer { get; set; }

    /// <summary>
    /// Per-field rate limit service. When set, every selected field tagged with
    /// <see cref="QueryLimits.FieldRateLimitExtension"/> (via <c>field.AddRateLimit(policy)</c>) has a permit
    /// acquired before execution and released after. If null and no service is registered in DI, field
    /// rate limiting is disabled even on tagged fields — the tag becomes a no-op.
    /// </summary>
    public IFieldRateLimitService? FieldRateLimitService { get; set; }

    /// <summary>
    /// Selects a user key for rate limiters tagged <c>userSpecific: true</c>. Default selector uses
    /// <c>ClaimsPrincipal.Identity?.Name</c>; override for services that key on a claim or API-key header.
    /// Return null to disable per-user partitioning for the current request (request then hits the shared
    /// partition).
    /// </summary>
    public Func<ClaimsPrincipal?, string?>? UserKeySelector { get; set; }

#if DEBUG
    /// <summary>
    /// Include timing information about query execution
    /// </summary>
    /// <value></value>
    public bool IncludeDebugInfo { get; set; }

    /// <summary>
    /// Do not execute the expression. Used for performance testing on EntityGraphQL code
    /// </summary>
    /// <value></value>
    public bool NoExecution { get; set; }
#endif
}
