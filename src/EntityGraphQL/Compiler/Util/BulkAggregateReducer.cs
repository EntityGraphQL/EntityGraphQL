using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// Builds the pass-2 reduction for aggregating a bulk-resolved element field. Instead of calling the per-element
/// fallback once per row, it invokes the bulk fetch a single time over the materialized keys, then reduces the
/// returned dictionary:
///   var dict = fetch(keys.Distinct(), svc);
///   return keys.Select(k => dict[k]).{Sum|Min|Max|Average}();
/// </summary>
public static class BulkAggregateReducer
{
    /// <summary>
    /// <paramref name="materializedKeys"/> is an in-memory IEnumerable&lt;TKey&gt; (the deps materialized in pass 1).
    /// <paramref name="fetch"/> is the bulk resolver's <c>(IEnumerable&lt;TKey&gt; keys, TService svc) =&gt; IDictionary&lt;TKey, TResult&gt;</c>
    /// lambda; its service parameter must be registered in the field's Services so it is injected at execution.
    /// </summary>
    public static Expression Build(Expression materializedKeys, LambdaExpression fetch, string method)
    {
        var keysParam = fetch.Parameters[0];
        var dictType = fetch.Body.Type; // IDictionary<TKey, TResult>
        var keyType = dictType.GetGenericArguments()[0];
        var valueType = dictType.GetGenericArguments()[1];

        // dict = fetch(keys.Distinct(), svc) — the service param stays and is injected by the framework
        var distinctKeys = Expression.Call(typeof(Enumerable), nameof(Enumerable.Distinct), [keyType], materializedKeys);
        var dictExpr = new ParameterReplacer().Replace(fetch.Body, keysParam, distinctKeys);
        var dictVar = Expression.Variable(dictType, "bulkDict");

        // values = keys.Select(k => dict[k]) — nullable for Min/Max/Average so an empty set yields null (not throw)
        var kParam = Expression.Parameter(keyType, "k");
        Expression lookup = Expression.Property(dictVar, "Item", kParam);
        if (method != nameof(Enumerable.Sum) && valueType.IsValueType && Nullable.GetUnderlyingType(valueType) == null)
            lookup = Expression.Convert(lookup, typeof(Nullable<>).MakeGenericType(valueType));

        var values = Expression.Call(typeof(Enumerable), nameof(Enumerable.Select), [keyType, lookup.Type], materializedKeys, Expression.Lambda(lookup, kParam));

        // Min/Max (no selector) are generic on the element type; Sum/Average resolve by the element type overload
        Expression agg =
            method is nameof(Enumerable.Min) or nameof(Enumerable.Max)
                ? Expression.Call(typeof(Enumerable), method, [lookup.Type], values)
                : Expression.Call(typeof(Enumerable), method, Type.EmptyTypes, values);

        return Expression.Block([dictVar], Expression.Assign(dictVar, dictExpr), agg);
    }
}
