using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Extensions
{
    internal class WhereWithParamVisitor : ExpressionVisitor
    {
        private readonly IQueryProvider provider;

        internal WhereWithParamVisitor(IQueryProvider provider)
        {
            this.provider = provider;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            return base.VisitMethodCall(node);
        }

    }
    internal class WhereWithParamQuery<TElement> : IQueryable<TElement>
    {
        public WhereWithParamQuery(WhereWithParamQueryProvider whereWithParamQueryProvider, Expression expression)
        {
            Provider = whereWithParamQueryProvider;
            Expression = expression;
        }

        public Type ElementType => typeof(TElement);

        public Expression Expression { get; }

        public IQueryProvider Provider { get; }


        public IEnumerator<TElement> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
    internal class WhereWithParamQueryProvider : IQueryProvider
    {
        private readonly IQueryProvider underlyingQueryProvider;

        internal WhereWithParamQueryProvider(IQueryProvider underlyingQueryProvider)
        {
            this.underlyingQueryProvider = underlyingQueryProvider;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            Type elementType = expression.Type.GetElementType();
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(WhereWithParamQuery<>).MakeGenericType(elementType), new object[] { this, expression });
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new WhereWithParamQuery<TElement>(this, expression);
        }

        public object Execute(Expression expression)
        {
            return underlyingQueryProvider.Execute(Visit(expression));
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return underlyingQueryProvider.Execute<TResult>(Visit(expression));
        }

        private Expression Visit(Expression expression)
        {
            var visitor = new WhereWithParamVisitor(underlyingQueryProvider);
            var exp = visitor.Visit(expression);
            return exp;
        }
    }
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

        public static IQueryable<TSource> AsConstSupported<TSource>(this IQueryable<TSource> source)
        {
            return new WhereWithParamQueryProvider(source.Provider).CreateQuery<TSource>(source.Expression);
        }

        public static IEnumerable<TSource> Take<TSource>(this IEnumerable<TSource> source, int? count)
        {
            if (!count.HasValue)
                return source;

            return Enumerable.Take(source, count.Value);
        }

        public static IQueryable<TSource> WhereWhen<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> wherePredicate, bool applyPredicate)
        {
            if (applyPredicate)
                return Queryable.Where(source, wherePredicate);

            return source;
        }

        public static IEnumerable<TSource> WhereWhen<TSource>(this IEnumerable<TSource> source, Expression<Func<TSource, bool>> wherePredicate, bool applyPredicate)
        {
            if (applyPredicate)
                return Queryable.Where(source.AsQueryable(), wherePredicate).AsEnumerable();

            return source;
        }

        public static IEnumerable<TSource> WhereWhen<TSource>(this IEnumerable<TSource> source, EntityQueryType<TSource> filter, bool applyPredicate)
        {
            if (filter.HasValue && applyPredicate)
                return Queryable.Where(source.AsQueryable(), filter.Query);
            return source;
        }
        public static IQueryable<TSource> WhereWhen<TSource>(this IQueryable<TSource> source, EntityQueryType<TSource> filter, bool applyPredicate)
        {
            if (filter.HasValue && applyPredicate)
                return Queryable.Where(source, filter.Query);
            return source;
        }
    }
}