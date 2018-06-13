using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityQueryLanguage.Extensions
{
    /// Extension methods to allow EQL compiled expression to easily be used in LINQ methods.
    /// TODO: fill this out with all the others. Also wonder if there is a better way?
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
        public static IEnumerable<TSource> LongCount<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "LongCount", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
        }
        public static IEnumerable<TSource> TakeWhile<TSource>(this IEnumerable<TSource> source, LambdaExpression predicate)
        {
            var call = Expression.Call(typeof(Enumerable), "TakeWhile", new[] { typeof(TSource) }, Expression.Constant(source), predicate);
            return (IEnumerable<TSource>)Expression.Lambda(call).Compile().DynamicInvoke();
        }
    }
}