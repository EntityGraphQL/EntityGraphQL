using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Extensions
{
    /// <summary>
    /// Extension methods to allow to allow you to build queries and reuse expressions/filters
    /// </summary>
    public static class EnumerableExtensions
    {
        public static IEnumerable<TSource> Take<TSource>(this IEnumerable<TSource> source, int? count)
        {
            if (!count.HasValue)
                return source;

            return Enumerable.Take(source, count.Value);
        }
        public static IEnumerable<TSource> Skip<TSource>(this IEnumerable<TSource> source, int? count)
        {
            if (!count.HasValue)
                return source;

            return Enumerable.Skip(source, count.Value);
        }
        public static IEnumerable<TSource> WhereWhen<TSource>(this IEnumerable<TSource> source, Expression<Func<TSource, bool>> wherePredicate, bool applyPredicate)
        {
            if (applyPredicate)
                return Queryable.Where(source.AsQueryable(), wherePredicate).AsEnumerable();

            return source;
        }

        public static IEnumerable<TSource> WhereWhen<TSource>(this IEnumerable<TSource> source, EntityQueryType<TSource> filter, bool applyPredicate)
        {
            if (applyPredicate)
                return Queryable.Where(source.AsQueryable(), filter.Query!);
            return source;
        }

        /// <summary>
        /// Does a null check on source. Returns null if source is null, otherwise returns source.ToList()
        /// If returnEmptyList is true, returns an empty list if source is null
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"></param>
        /// <param name="returnEmptyList"></param>
        /// <returns></returns>
        public static List<TSource>? ToListWithNullCheck<TSource>(this IEnumerable<TSource> source, bool returnEmptyList)
        {
            if (source == null)
                return returnEmptyList ? new List<TSource>() : null;
            return source.ToList();
        }

        public static IEnumerable<TResult>? SelectWithNullCheck<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            if (source == null)
                return null;
            return source.Select(selector);
        }
    }
}