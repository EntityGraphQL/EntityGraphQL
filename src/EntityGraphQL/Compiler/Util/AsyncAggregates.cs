using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// The request's concurrency limits and cancellation for an async aggregate. Bound per request as a constant
/// parameter, as the concurrency limit extension binds its registry and token, so a cached delegate never holds an
/// earlier request's.
/// </summary>
internal sealed class AsyncAggregateContext(
    ConcurrencyLimiterRegistry registry,
    List<(string scopeKey, int maxConcurrency)> limits,
    CancellationToken cancellationToken,
    IField elementField,
    ConcurrentDictionary<object, Lazy<Task>> values
)
{
    public Task<T> RunAsync<T>(Func<Task<T>> operation) => ConcurrencyLimitFieldExtension.RunWithLimitsAsync(operation, limits, registry, cancellationToken);

    /// <summary>
    /// The element field's values for this source - fetched by the first function that asks (sum, max, ...), shared
    /// with the rest. Each function's pass 1 fetched its own list, so the lists are matched by their items. Lazy so
    /// two asking at once fetch once
    /// </summary>
    public Task<T> ValuesFor<TItem, T>(IEnumerable<TItem> source, Func<Task<T>> fetch) =>
        (Task<T>)values.GetOrAdd(new SourceKey<TItem>(elementField, source), _ => new Lazy<Task>(fetch)).Value;

    private sealed class SourceKey<TItem> : IEquatable<SourceKey<TItem>>
    {
        // a deps type (pass 1's projection of the members a service reads) is built at runtime without an Equals of
        // its own - compare its fields. Bulk keys are the key values themselves
        private static readonly FieldInfo[]? depsFields = typeof(TItem).Assembly.IsDynamic ? typeof(TItem).GetFields() : null;

        private readonly IField field;
        private readonly TItem[] items;
        private readonly int hash;

        public SourceKey(IField field, IEnumerable<TItem> source)
        {
            this.field = field;
            items = [.. source];
            var h = new HashCode();
            h.Add(field);
            foreach (var item in items)
                h.Add(ItemHash(item));
            hash = h.ToHashCode();
        }

        public bool Equals(SourceKey<TItem>? other) =>
            other != null && other.field == field && other.items.Length == items.Length && items.Zip(other.items).All(p => ItemEquals(p.First, p.Second));

        public override bool Equals(object? obj) => Equals(obj as SourceKey<TItem>);

        public override int GetHashCode() => hash;

        private static bool ItemEquals(TItem a, TItem b) =>
            depsFields == null ? EqualityComparer<TItem>.Default.Equals(a, b) : depsFields.All(f => Equals(f.GetValue(a), f.GetValue(b)));

        private static int ItemHash(TItem item)
        {
            if (item is null)
                return 0;
            if (depsFields == null)
                return EqualityComparer<TItem>.Default.GetHashCode(item);
            var h = new HashCode();
            foreach (var f in depsFields)
                h.Add(f.GetValue(item));
            return h.ToHashCode();
        }
    }
}

/// <summary>
/// Aggregates an async element field (Sum, Min, ...) in the services pass. The engine awaits the returned task as
/// it does any async field.
/// </summary>
internal static class AsyncAggregates
{
    /// <summary>
    /// AsyncAggregates.Reduce(source, selector, reduce, context) - selector is the element's async value, reduce the
    /// aggregate function over the awaited values
    /// </summary>
    public static MethodCallExpression CallReduce(Expression source, LambdaExpression selector, LambdaExpression reduce, Expression context) =>
        Expression.Call(
            typeof(AsyncAggregates),
            nameof(Reduce),
            [source.Type.GetEnumerableOrArrayType()!, selector.ReturnType.GetGenericArguments()[0], reduce.ReturnType],
            source,
            selector,
            reduce,
            context
        );

    /// <summary>A placeholder for the context in the schema's expression - each request binds its own</summary>
    public static readonly Expression NoContext = Expression.Constant(null, typeof(AsyncAggregateContext));

    public static async Task<TResult> Reduce<TSource, TValue, TResult>(
        IEnumerable<TSource> source,
        Func<TSource, Task<TValue>> selector,
        Func<IEnumerable<TValue>, TResult> reduce,
        AsyncAggregateContext? context
    )
    {
        var values = context != null ? await context.ValuesFor(source, () => FetchAll(source, selector, context)) : await FetchAll(source, selector, null);
        return reduce(values);
    }

    // each element runs under the field's concurrency limits, as the field does in a list
    private static Task<TValue[]> FetchAll<TSource, TValue>(IEnumerable<TSource> source, Func<TSource, Task<TValue>> selector, AsyncAggregateContext? context) =>
        Task.WhenAll(source.Select(item => context != null ? context.RunAsync(() => selector(item)) : selector(item)));

    /// <summary>
    /// One call to an async bulk resolver for the keys pass 1 fetched (shared by each function of the field), then
    /// the aggregate function over its results
    /// </summary>
    public static async Task<TResult> ReduceBulk<TKey, TDictionary, TResult>(IEnumerable<TKey> keys, Func<Task<TDictionary>> fetch, Func<TDictionary, TResult> reduce, AsyncAggregateContext? context) =>
        reduce(await (context != null ? context.ValuesFor(keys, () => context.RunAsync(fetch)) : fetch()));
}
