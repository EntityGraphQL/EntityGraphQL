using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Walks the compiled query tree once, counts the number of selections per <c>(policy, userKey)</c> tuple,
/// and acquires that many permits in a single call to <see cref="IFieldRateLimitService.TryAcquireAsync"/>.
/// Returns an aggregate lease the caller disposes after execution.
///
/// Each selection of a rate-limited field counts — aliases and fragment spreads are <em>not</em> deduplicated.
/// This matches the "rate-limit the resolver invocation" semantics: selecting an expensive field 10 times
/// in one query runs the resolver 10 times and should charge 10 permits. Consumers who want once-per-request
/// semantics configure that on the policy side; services honor it by clamping <see cref="FieldRateLimitRequest.PermitCount"/>.
///
/// Lease handling: concurrency limiters only release a permit on lease dispose, so the caller must always
/// dispose in a finally, even if execution itself throws.
/// </summary>
internal static class FieldRateLimitExecutor
{
    internal static async ValueTask<AggregateFieldRateLimitLease?> AcquireAsync(
        GraphQLDocument document,
        string? operationName,
        IFieldRateLimitService service,
        ClaimsPrincipal? user,
        Func<ClaimsPrincipal?, string?>? userKeySelector,
        CancellationToken cancellationToken
    )
    {
        var op = string.IsNullOrEmpty(operationName) ? (document.Operations.Count > 0 ? document.Operations[0] : null) : document.Operations.Find(o => o.Name == operationName);
        if (op == null)
            return null;

        // Count selections per (policy, userKey). One acquire call per tuple, but with permitCount = count.
        var counts = new Dictionary<(string policy, string? userKey), int>();
        var visitedFragments = new HashSet<string>(StringComparer.Ordinal);
        string? cachedUserKey = null;
        var cachedUserKeyComputed = false;

        foreach (var field in op.QueryFields)
            Collect(field, counts, visitedFragments, document.Fragments, user, userKeySelector, ref cachedUserKey, ref cachedUserKeyComputed);

        if (counts.Count == 0)
            return null;

        var leases = new List<IFieldRateLimitLease>(counts.Count);
        var aggregate = new AggregateFieldRateLimitLease(leases);
        try
        {
            foreach (var ((policy, userKey), count) in counts)
            {
                var lease = await service.TryAcquireAsync(new FieldRateLimitRequest(policy, userKey, count, cancellationToken)).ConfigureAwait(false);
                leases.Add(lease);
                if (!lease.IsAcquired)
                    throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Rate limit exceeded for policy '{policy}'");
            }
            return aggregate;
        }
        catch
        {
            aggregate.Dispose();
            throw;
        }
    }

    private static void Collect(
        BaseGraphQLField field,
        Dictionary<(string policy, string? userKey), int> acc,
        HashSet<string> visitedFragments,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ClaimsPrincipal? user,
        Func<ClaimsPrincipal?, string?>? userKeySelector,
        ref string? cachedUserKey,
        ref bool cachedUserKeyComputed
    )
    {
        if (field is GraphQLFragmentSpreadField spread)
        {
            if (!fragments.TryGetValue(spread.Name, out var fragment))
                return;
            if (!visitedFragments.Add(spread.Name))
                return;
            foreach (var child in fragment.QueryFields)
                Collect(child, acc, visitedFragments, fragments, user, userKeySelector, ref cachedUserKey, ref cachedUserKeyComputed);
            return;
        }

        if (field is GraphQLInlineFragmentField inline)
        {
            foreach (var child in inline.QueryFields)
                Collect(child, acc, visitedFragments, fragments, user, userKeySelector, ref cachedUserKey, ref cachedUserKeyComputed);
            return;
        }

        if (field.Field != null)
        {
            foreach (var ext in field.Field.Extensions)
            {
                if (ext is FieldRateLimitExtension rl)
                {
                    string? userKey = null;
                    if (rl.UserSpecific)
                    {
                        if (!cachedUserKeyComputed)
                        {
                            cachedUserKey = ResolveUserKey(user, userKeySelector);
                            cachedUserKeyComputed = true;
                        }
                        userKey = cachedUserKey;
                    }
                    var key = (rl.PolicyName, userKey);
                    acc[key] = acc.TryGetValue(key, out var existing) ? existing + 1 : 1;
                }
            }
        }

        foreach (var child in field.QueryFields)
            Collect(child, acc, visitedFragments, fragments, user, userKeySelector, ref cachedUserKey, ref cachedUserKeyComputed);
    }

    private static string? ResolveUserKey(ClaimsPrincipal? user, Func<ClaimsPrincipal?, string?>? selector)
    {
        if (selector != null)
            return selector(user);
        return user?.Identity?.Name;
    }
}
