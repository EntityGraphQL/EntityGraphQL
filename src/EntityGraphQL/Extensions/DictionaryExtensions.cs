using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Extensions;

public static class DictionaryExtensions
{
    public static Dictionary<TKey, TElement> MergeNew<TKey, TElement>(this IDictionary<TKey, TElement> source, IReadOnlyDictionary<TKey, TElement>? other) where TKey : notnull
    {
        var result = source != null ? source.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) : new Dictionary<TKey, TElement>();
        if (other != null)
            foreach (var kvp in other)
                result[kvp.Key] = kvp.Value;

        return result;
    }
}