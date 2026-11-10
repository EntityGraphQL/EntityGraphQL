using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// Aggregates an async element field: awaits each element's value, then reduces them (Sum, Min, ...). The engine
/// awaits the returned task as it does any async field.
/// </summary>
internal static class AsyncAggregates
{
    public static async Task<TResult> Reduce<TSource, TValue, TResult>(IEnumerable<TSource> source, Func<TSource, Task<TValue>> selector, Func<IEnumerable<TValue>, TResult> reduce)
    {
        // ponytail: one element at a time, so a service that is not thread-safe (a scoped DbContext) is safe to use.
        // Await concurrently (under the concurrency limits) if large collections are slow
        var values = new List<TValue>();
        foreach (var item in source)
            values.Add(await selector(item));
        return reduce(values);
    }
}
