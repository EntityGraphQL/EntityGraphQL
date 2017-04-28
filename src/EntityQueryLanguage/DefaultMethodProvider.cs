using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Util;

namespace EntityQueryLanguage
{
    /// The default method provider for Entity Query Language. Implements all the useful Linq functions for
    /// querying and filtering your data requests.
    ///
    /// Supported Methods:
    ///   List.where(filter)
    ///   List.filter(filter)
    ///   List.first(filter?)
    ///   List.last(filter?)
    ///   List.take(int)
    ///   List.skip(int)
    ///   List.count(filter?)
    ///   List.orderBy(field)
    ///   List.orderByDesc(field)
    ///
    ///   TODO:
    ///   List.sort(field desc?, ...)
    ///   List.select(field, ...)?
    ///   List.isBelow(primary_key)
    ///   List.isAtOrBelow(primary_key)
    ///   List.isAbove(primary_key)
    ///   List.isAtOrAbove(primary_key)
    ///   string.startsWith(string)
    ///   string.endsWith(string)
    public class DefaultMethodProvider : IMethodProvider
    {
        // Map of the method names and a function that makes the Expression.Call
        private Dictionary<string, Func<Expression, Expression, string, Expression[], Expression>> _supportedMethods = new Dictionary<string, Func<Expression, Expression, string, Expression[], Expression>>(StringComparer.OrdinalIgnoreCase)
    {
        { "where", MakeWhereMethod },
      { "filter", MakeWhereMethod },
      { "first", MakeFirstMethod },
      { "last", MakeLastMethod },
      { "take", MakeTakeMethod },
      { "skip", MakeSkipMethod },
      { "count", MakeCountMethod },
      { "orderby", MakeOrderByMethod },
      { "orderbydesc", MakeOrderByDescMethod },
    };

        public bool EntityTypeHasMethod(Type context, string methodName)
        {
            return _supportedMethods.ContainsKey(methodName);
        }

        public Expression GetMethodContext(Expression context, string methodName)
        {
            // some methods have a context of the element type in the list, other is jsut the original context
            // need some way for the method compiler to tells us that
            //  return _supportedMethods[methodName](context);
            return GetContextFromEnumerable(context);
        }

        public Expression MakeCall(Expression context, Expression argContext, string methodName, IEnumerable<Expression> args)
        {
            if (_supportedMethods.ContainsKey(methodName))
            {
                return _supportedMethods[methodName](context, argContext, methodName, args != null ? args.ToArray() : new Expression[] { });
            }
            throw new EqlCompilerException($"Unsupported method {methodName}");
        }

        private static Expression MakeWhereMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var predicate = args.First();
            ExpectArgTypeToBe(predicate.Type, typeof(bool), methodName);
            var lambda = Expression.Lambda(predicate, argContext as ParameterExpression);
            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "Where", new Type[] { argContext.Type }, context, lambda);
        }

        private static Expression MakeFirstMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, "First");
        }

        private static Expression MakeCountMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, "Count");
        }

        private static Expression MakeOptionalFilterArgumentCall(Expression context, Expression argContext, string methodName, Expression[] args, string actualMethodName)
        {
            ExpectArgsCountBetween(0, 1, args, methodName);

            var allArgs = new List<Expression> { context };
            if (args.Count() == 1)
            {
                var predicate = args.First();
                ExpectArgTypeToBe(predicate.Type, typeof(bool), methodName);
                allArgs.Add(Expression.Lambda(predicate, argContext as ParameterExpression));
            }

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, actualMethodName, new Type[] { argContext.Type }, allArgs.ToArray());
        }

        private static Expression MakeLastMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, "Last");
        }

        private static Expression MakeTakeMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var amount = args.First();
            ExpectArgTypeToBe(amount.Type, typeof(int), methodName);

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "Take", new Type[] { argContext.Type }, context, amount);
        }

        private static Expression MakeSkipMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var amount = args.First();
            ExpectArgTypeToBe(amount.Type, typeof(int), methodName);

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "Skip", new Type[] { argContext.Type }, context, amount);
        }

        private static Expression MakeOrderByMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var column = args.First();
            var lambda = Expression.Lambda(column, argContext as ParameterExpression);

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "OrderBy", new Type[] { argContext.Type, column.Type }, context, lambda);
        }

        private static Expression MakeOrderByDescMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var column = args.First();
            var lambda = Expression.Lambda(column, argContext as ParameterExpression);

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "OrderByDescending", new Type[] { argContext.Type, column.Type }, context, lambda);
        }

        private static Expression GetContextFromEnumerable(Expression context)
        {
            if (context.Type.IsEnumerable())
            {
                return Expression.Parameter(context.Type.GetGenericArguments()[0]);
            }
            var t = context.Type.GetEnumerableType();
            if (t != null)
                return Expression.Parameter(t);
            return context;
        }

        private static void ExpectArgsCount(int count, Expression[] args, string method)
        {
            if (args.Count() != count)
                throw new EqlCompilerException($"Method '{method}' expects {count} argument(s) but {args.Count()} were supplied");
        }

        private static void ExpectArgsCountBetween(int low, int high, Expression[] args, string method)
        {
            if (args.Count() < low || args.Count() > high)
                throw new EqlCompilerException($"Method '{method}' expects {low}-{high} argument(s) but {args.Count()} were supplied");
        }

        private static void ExpectArgTypeToBe(Type argType, Type expected, string methodName)
        {
            if (argType != expected)
                throw new EqlCompilerException($"Method '{methodName}' expects parameter that evaluates to a '{expected}' result but found result type '{argType}'");
        }
    }
}
