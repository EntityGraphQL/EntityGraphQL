using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Per-request registry for semaphores used in concurrency limiting
/// Request-scoped or application-scoped depending on your DI setup
/// </summary>
public class ConcurrencyLimiterRegistry
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> semaphores = new();

    public SemaphoreSlim GetSemaphore(string key, int maxConcurrency)
    {
        return semaphores.GetOrAdd(key, _ => new SemaphoreSlim(maxConcurrency, maxConcurrency));
    }

    /// <summary>
    /// Call this at the end of request processing to clean up per-request semaphores
    /// </summary>
    public void ClearRequestSemaphores()
    {
        foreach (var kvp in semaphores.ToList())
        {
            if (kvp.Key.StartsWith("field_", StringComparison.CurrentCultureIgnoreCase) || kvp.Key.StartsWith("query_", StringComparison.CurrentCultureIgnoreCase))
            {
                if (semaphores.TryRemove(kvp.Key, out var semaphore))
                {
                    semaphore.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// For testing or application shutdown
    /// </summary>
    public void ClearAllSemaphores()
    {
        foreach (var semaphore in semaphores.Values)
        {
            semaphore.Dispose();
        }
        semaphores.Clear();
    }
}
