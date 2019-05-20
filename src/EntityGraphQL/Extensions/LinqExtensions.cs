using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Extensions
{
    /// <summary>
    /// Extension methods to allow EQL compiled expression to easily be used in LINQ methods. Also helpers to work with nullable parmaeters
    /// </summary>
    public static class LinqExtensions
    {
        public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "Where", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
        }
        public static IEnumerable<TSource> Any<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "Any", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
        }
        public static IEnumerable<TSource> Count<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "Count", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
        }
        public static IEnumerable<TSource> First<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "First", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
        }
        public static IEnumerable<TSource> FirstOrDefault<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "FirstOrDefault", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
        }
        public static IEnumerable<TSource> Last<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "Last", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
        }
        public static IEnumerable<TSource> LastOrDefault<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "LastOrDefault", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
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