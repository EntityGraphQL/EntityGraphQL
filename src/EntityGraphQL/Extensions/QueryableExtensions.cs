using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Extensions
{
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

        public static IQueryable<TSource> WhereWhen<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> wherePredicate, bool applyPredicate)
        {
            if (applyPredicate)
                return Queryable.Where(source, wherePredicate);

            return source;
        }

        public static IQueryable<TSource> WhereWhen<TSource>(this IQueryable<TSource> source, EntityQueryType<TSource> filter, bool applyPredicate)
        {
            if (applyPredicate)
                return Queryable.Where(source, filter.Query!);
            return source;
        }

        public static IQueryable<TResult>? SelectWithNullCheck<TSource, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector)
        {
            if (source == null)
                return null;
            return source.Select(selector);
        }
    }
}