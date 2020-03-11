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
    public static class LinqExtensions
    {
        /// <summary>
        /// Applies the EntityQueryType<> filter expression against the source
        /// </summary>
        /// <param name="source"></param>
        /// <param name="filter"></param>
        /// <typeparam name="TSource"></typeparam>
        /// <returns></returns>
        public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, EntityQueryType<TSource> filter)
        {
            if (filter.HasValue)
                return Queryable.Where(source.AsQueryable(), filter.Query);
            return source;
        }

        public static IQueryable<TSource> Take<TSource>(this IQueryable<TSource> source, int? count)
        {
            if (!count.HasValue)
                return source;

            return Queryable.Take(source, count.Value);
        }

        /// <summary>
        /// Apply the Where condition when applyPredicate is true
        /// </summary>
        /// <param name="source"></param>
        /// <param name="wherePredicate"></param>
        /// <param name="applyPredicate"></param>
        /// <typeparam name="TSource"></typeparam>
        /// <returns></returns>
        public static IQueryable<TSource> WhereWhen<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> wherePredicate, bool applyPredicate)
        {
            if (applyPredicate)
                return Queryable.Where(source, wherePredicate);

            return source;
        }

        /// <summary>
        /// Apply the Where condition when applyPredicate is true
        /// </summary>
        /// <param name="source"></param>
        /// <param name="wherePredicate"></param>
        /// <param name="applyPredicate"></param>
        /// <typeparam name="TSource"></typeparam>
        /// <returns></returns>
        public static IQueryable<TSource> WhereWhen<TSource>(this IEnumerable<TSource> source, Expression<Func<TSource, bool>> wherePredicate, bool applyPredicate)
        {
            if (applyPredicate)
                return Queryable.Where(source.AsQueryable(), wherePredicate);

            return source.AsQueryable();
        }
    }
}