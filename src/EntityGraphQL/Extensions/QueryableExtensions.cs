using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EntityGraphQL.Extensions;

/// <summary>
/// Extension methods to allow to allow you to build queries and reuse expressions/filters
/// </summary>
public static class QueryableExtensions
{
    public static IQueryable<TSource> Take<TSource>(this IQueryable<TSource> source, int? count)
    {
        if (!count.HasValue)
            return source;

        return Queryable.Take(source, count.Value);
    }

    public static IQueryable<TSource> Skip<TSource>(this IQueryable<TSource> source, int? count)
    {
        if (!count.HasValue)
            return source;

        return Queryable.Skip(source, count.Value);
    }

    /// <summary>
    /// True if there are any items after the current page (skip + take). Used by the paging extensions to
    /// answer hasNextPage with a cheap EXISTS query instead of a full COUNT when the total is not requested.
    /// </summary>
    public static bool PageHasNext<TSource>(this IQueryable<TSource> source, int? skip, int? take)
    {
        if (!take.HasValue)
            return false; // no take = the page is the whole remaining collection
        return Queryable.Any(Queryable.Skip(source, (skip ?? 0) + take.Value));
    }

    public static IQueryable<TSource> WhereWhen<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> wherePredicate, bool applyPredicate)
    {
        if (applyPredicate)
            return Queryable.Where(source, wherePredicate);

        return source;
    }

    public static IQueryable<TResult>? SelectWithNullCheck<TSource, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector)
    {
        if (source == null)
            return null;
        return source.Select(selector);
    }

    public static async Task<IQueryable<TResult>?> SelectWithNullCheck<TSource, TResult>(this Task<IQueryable<TSource>> source, Expression<Func<TSource, TResult>> selector)
    {
        var awaitedSource = await source;
        if (awaitedSource == null)
            return null;
        return awaitedSource.Select(selector);
    }
}
