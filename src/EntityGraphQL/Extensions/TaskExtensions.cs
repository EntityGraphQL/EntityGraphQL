using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EntityGraphQL.Extensions;

public static class AsyncEnumerableHelpers
{
    /// <summary>
    /// Awaits a Task<IEnumerable<TSource>> and then applies a selector to each element
    /// This is used for async service fields that return collections with field selections
    /// </summary>
    public static async Task<IEnumerable<TResult>> SelectAsync<TSource, TResult>(Task<IEnumerable<TSource>> source, Func<TSource, TResult> selector)
    {
        var result = await source;
        if (result == null)
            return null!;
        return result.Select(selector);
    }

    /// <summary>
    /// Awaits a Task<IEnumerable<TSource>> and then applies a selector to each element with null checking
    /// </summary>
    public static async Task<IEnumerable<TResult>?> SelectAsyncWithNullCheck<TSource, TResult>(Task<IEnumerable<TSource>> source, Func<TSource, TResult> selector)
    {
        var result = await source;
        if (result == null)
            return null;
        return result.Select(selector);
    }
}
