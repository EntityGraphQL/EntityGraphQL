using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using EntityGraphQL.Schema.QueryLimits;

namespace EntityGraphQL.AspNet;

/// <summary>
/// Configures per-field rate-limit policies for the ASP.NET default <see cref="IFieldRateLimitService"/> backed by
/// <see cref="PartitionedRateLimiter{TResource}"/>. Policy names here must match the names passed to
/// <c>field.AddRateLimit("policy")</c>.
///
/// Partitioning: when a field is tagged <c>userSpecific: true</c>, EntityGraphQL forms a partition key of
/// <c>"policy|userKey"</c>; otherwise it uses the bare policy name. Each unique key gets its own limiter
/// instance — fine for a handful of policies and moderate user counts, but be mindful that partitions are
/// not evicted. For very large user populations, replace <see cref="IFieldRateLimitService"/> with a custom
/// implementation that evicts idle partitions or uses a distributed backend.
///
/// Counting semantics: each selection of a rate-limited field in a query acquires one permit. A query that
/// selects the same field 10 times (aliases, fragment spreads) acquires 10 permits. Pass
/// <c>oncePerRequest: true</c> when registering a policy to clamp that to 1 — useful for policies whose unit
/// is "queries per window" rather than "invocations per window".
/// </summary>
public class GraphQLFieldRateLimitOptions
{
    internal Dictionary<string, PolicyEntry> Policies { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Register a custom policy. The factory receives the final partition key (either the policy name or
    /// <c>"policy|userKey"</c>) and returns a <see cref="RateLimitPartition{TKey}"/> — use this for
    /// scenarios the built-in helpers don't cover (e.g. different limits per user tier).
    /// </summary>
    public GraphQLFieldRateLimitOptions AddPolicy(string policyName, Func<string, RateLimitPartition<string>> partitionFactory, bool oncePerRequest = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(partitionFactory);
        Policies[policyName] = new PolicyEntry(partitionFactory, oncePerRequest);
        return this;
    }

    /// <summary>
    /// Fixed-window limiter. All requests in the current window count against <paramref name="permitLimit"/>;
    /// the counter resets at the window boundary.
    /// </summary>
    public GraphQLFieldRateLimitOptions AddFixedWindowPolicy(string policyName, int permitLimit, TimeSpan window, int queueLimit = 0, bool oncePerRequest = false)
    {
        return AddPolicy(
            policyName,
            key =>
                RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = queueLimit,
                        AutoReplenishment = true,
                    }
                ),
            oncePerRequest
        );
    }

    /// <summary>
    /// Sliding-window limiter — smoother than fixed window at the window boundary.
    /// </summary>
    public GraphQLFieldRateLimitOptions AddSlidingWindowPolicy(string policyName, int permitLimit, TimeSpan window, int segmentsPerWindow = 8, int queueLimit = 0, bool oncePerRequest = false)
    {
        return AddPolicy(
            policyName,
            key =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    key,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        SegmentsPerWindow = segmentsPerWindow,
                        QueueLimit = queueLimit,
                        AutoReplenishment = true,
                    }
                ),
            oncePerRequest
        );
    }

    /// <summary>
    /// Token-bucket limiter. Tokens replenish at <paramref name="tokensPerPeriod"/> every
    /// <paramref name="replenishmentPeriod"/>. Good for bursty workloads that should be allowed to catch up.
    /// </summary>
    public GraphQLFieldRateLimitOptions AddTokenBucketPolicy(string policyName, int tokenLimit, TimeSpan replenishmentPeriod, int tokensPerPeriod, int queueLimit = 0, bool oncePerRequest = false)
    {
        return AddPolicy(
            policyName,
            key =>
                RateLimitPartition.GetTokenBucketLimiter(
                    key,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = tokenLimit,
                        ReplenishmentPeriod = replenishmentPeriod,
                        TokensPerPeriod = tokensPerPeriod,
                        QueueLimit = queueLimit,
                        AutoReplenishment = true,
                    }
                ),
            oncePerRequest
        );
    }

    /// <summary>
    /// Concurrency limiter. Holds <paramref name="permitLimit"/> simultaneous executions; permits release
    /// when the query completes (lease is held for the full request duration).
    /// </summary>
    public GraphQLFieldRateLimitOptions AddConcurrencyPolicy(string policyName, int permitLimit, int queueLimit = 0, bool oncePerRequest = false)
    {
        return AddPolicy(policyName, key => RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions { PermitLimit = permitLimit, QueueLimit = queueLimit }), oncePerRequest);
    }

    internal readonly record struct PolicyEntry(Func<string, RateLimitPartition<string>> Factory, bool OncePerRequest);
}
