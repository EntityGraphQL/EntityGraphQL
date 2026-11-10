using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// The request's concurrency limits and cancellation for an async aggregate. Bound per request as a constant
/// parameter, as the concurrency limit extension binds its registry and token, so a cached delegate never holds an
/// earlier request's.
/// </summary>
internal sealed class AsyncAggregateContext(ConcurrencyLimiterRegistry registry, List<(string scopeKey, int maxConcurrency)> limits, CancellationToken cancellationToken)
{
    public Task<T> RunAsync<T>(Func<Task<T>> operation) => ConcurrencyLimitFieldExtension.RunWithLimitsAsync(operation, limits, registry, cancellationToken);
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
        // each element runs under the field's concurrency limits, as the field does in a list
        var values = await Task.WhenAll(source.Select(item => context != null ? context.RunAsync(() => selector(item)) : selector(item)));
        return reduce(values);
    }

    /// <summary>One call to an async bulk resolver, then the aggregate function over its results</summary>
    public static async Task<TResult> ReduceBulk<TDictionary, TResult>(Func<Task<TDictionary>> fetch, Func<TDictionary, TResult> reduce, AsyncAggregateContext? context) =>
        reduce(await (context != null ? context.RunAsync(fetch) : fetch()));
}
