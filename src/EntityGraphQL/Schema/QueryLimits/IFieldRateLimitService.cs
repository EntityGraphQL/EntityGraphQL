using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Acquires permits for individual GraphQL fields tagged with a <see cref="FieldRateLimitExtension"/>.
/// Called once per request, before any resolver runs. Implementations must be thread-safe.
///
/// Concurrency limiters in particular need the returned lease to be held for the duration of execution —
/// EntityGraphQL disposes leases in a finally block after execution completes.
/// </summary>
public interface IFieldRateLimitService
{
    /// <summary>
    /// Try to acquire a permit for a single field invocation.
    /// </summary>
    /// <param name="request">The request-specific parameters (policy, optional user key, cancellation).</param>
    /// <returns>A lease. <see cref="IFieldRateLimitLease.IsAcquired"/> is false when the limit is exceeded.</returns>
    ValueTask<IFieldRateLimitLease> TryAcquireAsync(FieldRateLimitRequest request);
}

/// <summary>
/// A handle on a rate-limit permit. Always dispose (even if not acquired) so partitioned limiters release
/// tracking state promptly.
/// </summary>
public interface IFieldRateLimitLease : IDisposable
{
    bool IsAcquired { get; }
}

/// <summary>
/// Parameters for a single rate-limit acquisition. Grouped in a struct so the interface can evolve without
/// breaking implementations.
/// </summary>
public readonly struct FieldRateLimitRequest
{
    public FieldRateLimitRequest(string policyName, string? userKey, int permitCount, CancellationToken cancellationToken)
    {
        PolicyName = policyName;
        UserKey = userKey;
        PermitCount = permitCount;
        CancellationToken = cancellationToken;
    }

    /// <summary>The policy name the field was tagged with (e.g. <c>"expensive-report"</c>).</summary>
    public string PolicyName { get; }

    /// <summary>Optional user key for per-user partitioning. Null when the field is not user-specific or no user is present.</summary>
    public string? UserKey { get; }

    /// <summary>
    /// Number of permits to acquire for this request. Defaults to the number of times the field was selected
    /// in the query (aliases and fragment spreads count separately). Implementations that want
    /// once-per-request semantics should clamp this to 1.
    /// </summary>
    public int PermitCount { get; }

    public CancellationToken CancellationToken { get; }
}

/// <summary>
/// A no-op lease used when a field has no matching policy configured (skip rather than deny).
/// </summary>
internal sealed class NoOpFieldRateLimitLease : IFieldRateLimitLease
{
    public static readonly NoOpFieldRateLimitLease Instance = new();

    public bool IsAcquired => true;

    public void Dispose() { }
}

/// <summary>
/// Aggregates multiple leases into one disposable so the execute path can do a single <c>finally</c> dispose.
/// </summary>
internal sealed class AggregateFieldRateLimitLease : IDisposable
{
    private readonly List<IFieldRateLimitLease> leases;

    public AggregateFieldRateLimitLease(List<IFieldRateLimitLease> leases)
    {
        this.leases = leases;
    }

    public void Dispose()
    {
        for (var i = leases.Count - 1; i >= 0; i--)
        {
            try
            {
                leases[i].Dispose();
            }
            catch
            {
                // swallow; disposing a rate-limit lease should never block the response path
            }
        }
    }
}
