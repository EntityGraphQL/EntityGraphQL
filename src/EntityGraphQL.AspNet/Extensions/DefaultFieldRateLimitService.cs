using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using EntityGraphQL.Schema.QueryLimits;
using Microsoft.Extensions.Options;

namespace EntityGraphQL.AspNet;

/// <summary>
/// In-memory <see cref="IFieldRateLimitService"/> backed by <see cref="PartitionedRateLimiter{TResource}"/>.
/// Registered by <see cref="EntityGraphQLAspNetRateLimitExtensions.AddGraphQLFieldRateLimit"/>.
///
/// Single-node only. For distributed scenarios (multiple app instances sharing a bucket) replace this
/// registration with a Redis- or database-backed implementation of <see cref="IFieldRateLimitService"/>.
/// </summary>
public sealed class DefaultFieldRateLimitService : IFieldRateLimitService, IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter;
    private readonly Dictionary<string, GraphQLFieldRateLimitOptions.PolicyEntry> policies;

    public DefaultFieldRateLimitService(IOptions<GraphQLFieldRateLimitOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var opts = options.Value;
        // Snapshot policies at construction so the factory we hand to PartitionedRateLimiter is stable for
        // the lifetime of the service — mutating options at runtime is not supported.
        policies = new Dictionary<string, GraphQLFieldRateLimitOptions.PolicyEntry>(opts.Policies, StringComparer.Ordinal);

        limiter = PartitionedRateLimiter.Create<string, string>(key =>
        {
            var policyName = ExtractPolicyName(key);
            if (policies.TryGetValue(policyName, out var entry))
                return entry.Factory(key);
            // Unreachable — TryAcquireAsync throws on unknown policies before reaching here.
            return RateLimitPartition.GetNoLimiter<string>(key);
        });
    }

    public async ValueTask<IFieldRateLimitLease> TryAcquireAsync(FieldRateLimitRequest request)
    {
        if (!policies.TryGetValue(request.PolicyName, out var entry))
        {
            throw new InvalidOperationException(
                $"A field is tagged with rate-limit policy '{request.PolicyName}' but no such policy is registered. "
                    + "Register it via AddGraphQLFieldRateLimit(opts => opts.AddFixedWindowPolicy(...)) or remove the field tag."
            );
        }

        var key = request.UserKey is null ? request.PolicyName : string.Concat(request.PolicyName, "|", request.UserKey);
        var permits = entry.OncePerRequest ? 1 : Math.Max(1, request.PermitCount);
        try
        {
            var lease = await limiter.AcquireAsync(key, permits, request.CancellationToken).ConfigureAwait(false);
            return new LeaseAdapter(lease);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Requested more permits than the bucket's total capacity. PartitionedRateLimiter signals this
            // via exception rather than an un-acquired lease, but from the caller's perspective it's a
            // denial — they asked for more than the policy ever permits in a single request.
            return new DeniedLease();
        }
    }

    public void Dispose() => limiter.Dispose();

    private static string ExtractPolicyName(string partitionKey)
    {
        var idx = partitionKey.IndexOf('|');
        return idx < 0 ? partitionKey : partitionKey.Substring(0, idx);
    }

    private sealed class LeaseAdapter : IFieldRateLimitLease
    {
        private readonly RateLimitLease inner;

        public LeaseAdapter(RateLimitLease inner)
        {
            this.inner = inner;
        }

        public bool IsAcquired => inner.IsAcquired;

        public void Dispose() => inner.Dispose();
    }

    private sealed class DeniedLease : IFieldRateLimitLease
    {
        public bool IsAcquired => false;

        public void Dispose() { }
    }
}
