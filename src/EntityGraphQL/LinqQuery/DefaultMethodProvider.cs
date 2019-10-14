using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using EntityGraphQL.Extensions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.LinqQuery
{
    /// <summary>
    /// The default method provider for Ling Querys. Implements the useful Linq functions for
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
    /// </summary>
    public class DefaultMethodProvider : IMethodProvider
    {
        // Map of the method names and a function that makes the Expression.Call
        private readonly Dictionary<string, Func<Expression, Expression, string, ExpressionResult[], ExpressionResult>> _supportedMethods = new Dictionary<string, Func<Expression, Expression, string, ExpressionResult[], ExpressionResult>>(StringComparer.OrdinalIgnoreCase)
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

        public ExpressionResult GetMethodContext(ExpressionResult context, string methodName)
        {
            // some methods have a context of the element type in the list, other is just the original context
            // need some way for the method compiler to tells us that
            //  return _supportedMethods[methodName](context);
            return GetContextFromEnumerable(context);
        }

        public ExpressionResult MakeCall(Expression context, Expression argContext, string methodName, IEnumerable<ExpressionResult> args)
        {
            if (_supportedMethods.ContainsKey(methodName))
            {
                return _supportedMethods[methodName](context, argContext, methodName, args != null ? args.ToArray() : new ExpressionResult[] { });
            }
            throw new EntityGraphQLCompilerException($"Unsupported method {methodName}");
        }

        private static ExpressionResult MakeWhereMethod(Expression context, Expression argContext, string methodName, ExpressionResult[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var predicate = args.First();
            predicate = ConvertTypeIfWeCan(methodName, predicate, typeof(bool));
            var lambda = Expression.Lambda(predicate, argContext as ParameterExpression);
            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "Where", new Type[] { argContext.Type }, context, lambda);
        }

        private static ExpressionResult MakeFirstMethod(Expression context, Expression argContext, string methodName, ExpressionResult[] args)
        {
            return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, "First");
        }

        private static ExpressionResult MakeCountMethod(Expression context, Expression argContext, string methodName, ExpressionResult[] args)
        {
            return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, "Count");
        }

        private static ExpressionResult MakeOptionalFilterArgumentCall(Expression context, Expression argContext, string methodName, ExpressionResult[] args, string actualMethodName)
        {
            ExpectArgsCountBetween(0, 1, args, methodName);

            var allArgs = new List<Expression> { context };
            if (args.Count() == 1)
            {
                var predicate = args.First();
                predicate = ConvertTypeIfWeCan(methodName, predicate, typeof(bool));
                allArgs.Add(Expression.Lambda(predicate, argContext as ParameterExpression));
            }

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, actualMethodName, new Type[] { argContext.Type }, allArgs.ToArray());
        }

        private static ExpressionResult MakeLastMethod(Expression context, Expression argContext, string methodName, ExpressionResult[] args)
        {
            return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, "Last");
        }

        private static ExpressionResult MakeTakeMethod(Expression context, Expression argContext, string methodName, ExpressionResult[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var amount = args.First();
            amount = ConvertTypeIfWeCan(methodName, amount, typeof(int));

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "Take", new Type[] { argContext.Type }, context, amount);
        }

        private static ExpressionResult MakeSkipMethod(Expression context, Expression argContext, string methodName, ExpressionResult[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var amount = args.First();
            amount = ConvertTypeIfWeCan(methodName, amount, typeof(int));

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "Skip", new Type[] { argContext.Type }, context, amount);
        }

        private static ExpressionResult MakeOrderByMethod(Expression context, Expression argContext, string methodName, ExpressionResult[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var column = args.First();
            var lambda = Expression.Lambda(column, argContext as ParameterExpression);

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "OrderBy", new Type[] { argContext.Type, column.Type }, context, lambda);
        }

        private static ExpressionResult MakeOrderByDescMethod(Expression context, Expression argContext, string methodName, ExpressionResult[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var column = args.First();
            var lambda = Expression.Lambda(column, argContext as ParameterExpression);

            return ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "OrderByDescending", new Type[] { argContext.Type, column.Type }, context, lambda);
        }

        private static ExpressionResult GetContextFromEnumerable(ExpressionResult context)
        {
            if (context.Type.IsEnumerableOrArray())
            {
                return (ExpressionResult)Expression.Parameter(context.Type.GetGenericArguments()[0]);
            }
            var t = context.Type.GetEnumerableOrArrayType();
            if (t != null)
                return (ExpressionResult)Expression.Parameter(t);
            return context;
        }

        private static void ExpectArgsCount(int count, ExpressionResult[] args, string method)
        {
            if (args.Count() != count)
                throw new EntityGraphQLCompilerException($"Method '{method}' expects {count} argument(s) but {args.Count()} were supplied");
        }

        private static void ExpectArgsCountBetween(int low, int high, ExpressionResult[] args, string method)
        {
            if (args.Count() < low || args.Count() > high)
                throw new EntityGraphQLCompilerException($"Method '{method}' expects {low}-{high} argument(s) but {args.Count()} were supplied");
        }

        private static ExpressionResult ConvertTypeIfWeCan(string methodName, ExpressionResult argExp, Type expected)
        {
            if (expected != argExp.Type)
            {
                try
                {
                    return (ExpressionResult)Expression.Convert(argExp, expected);
                }
                catch (Exception)
                {
                    throw new EntityGraphQLCompilerException($"Method '{methodName}' expects parameter that evaluates to a '{expected}' result but found result type '{argExp.Type}'");
                }
            }
            return argExp;
        }
    }
}
