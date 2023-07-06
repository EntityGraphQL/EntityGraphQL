using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using EntityGraphQL.Extensions;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Compiler.EntityQuery
{
    /// <summary>
    /// The default method provider for Ling Queries. Implements the useful Linq functions for
    /// querying and filtering your data requests.
    ///
    /// Supported Methods:
    ///   List.where(filter)
    ///   List.filter(filter)
    ///   List.first(filter?)
    ///   List.last(filter?)
    ///   List.any(filter)
    ///   List.take(int)
    ///   List.skip(int)
    ///   List.count(filter?)
    ///   List.orderBy(field)
    ///   List.orderByDesc(field)
    ///   string.contains(string)
    ///   string.startsWith(string)
    ///   string.endsWith(string)
    ///   string.toLower()
    ///   string.toUpper()
    ///
    ///   TODO:
    ///   List.select(field, ...)?
    ///   List.isBelow(primary_key)
    ///   List.isAtOrBelow(primary_key)
    ///   List.isAbove(primary_key)
    ///   List.isAtOrAbove(primary_key)
    /// </summary>
    public class DefaultMethodProvider : IMethodProvider
    {
        // Map of the method names and a function that makes the Expression.Call
        private readonly Dictionary<string, Func<Expression, Expression, string, Expression[], Expression>> supportedListMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            { "where", MakeWhereMethod },
            { "filter", MakeWhereMethod },
            { "first", MakeFirstMethod },
            { "last", MakeLastMethod },
            { "take", MakeTakeMethod },
            { "skip", MakeSkipMethod },
            { "count", MakeCountMethod },
            { "any", MakeAnyMethod },
            { "orderby", MakeOrderByMethod },
            { "orderbydesc", MakeOrderByDescMethod },
        };

        private readonly Dictionary<string, Func<Expression, string, Expression[], Expression>> supportedStringMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            { "contains", MakeStringContainsMethod },
            { "startsWith", MakeStringStartsWithMethod },
            { "endsWith", MakeStringEndsWithMethod },
            { "toLower", MakeStringToLowerMethod },
            { "toUpper", MakeStringToUpperMethod },
        };

        public bool EntityTypeHasMethod(Type context, string methodName)
        {
            if (context == typeof(string))
                return supportedStringMethods.ContainsKey(methodName);

            return supportedListMethods.ContainsKey(methodName);
        }

        public Expression GetMethodContext(Expression context, string methodName)
        {
            // some methods have a context of the element type in the list, other is just the original context
            // need some way for the method compiler to tells us that
            //  return _supportedMethods[methodName](context);
            return GetContextFromEnumerable(context);
        }

        public Expression MakeCall(Expression context, Expression argContext, string methodName, IEnumerable<Expression>? args, Type? type = null)
        {
            if (type == typeof(string) && supportedStringMethods.ContainsKey(methodName))
            {
                return supportedStringMethods[methodName](context, methodName, args != null ? args.ToArray() : Array.Empty<Expression>());
            }

            if (supportedListMethods.ContainsKey(methodName))
            {
                return supportedListMethods[methodName](context, argContext, methodName, args != null ? args.ToArray() : Array.Empty<Expression>());
            }
            throw new EntityGraphQLCompilerException($"Unsupported method {methodName}");
        }

        private static Expression MakeWhereMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var predicate = args.First();
            predicate = ConvertTypeIfWeCan(methodName, predicate, typeof(bool));
            var lambda = Expression.Lambda(predicate, (ParameterExpression)argContext);
            return ExpressionUtil.MakeCallOnQueryable("Where", new[] { argContext.Type }, context, lambda);
        }

        private static Expression MakeAnyMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var predicate = args.First();
            predicate = ConvertTypeIfWeCan(methodName, predicate, typeof(bool));
            var lambda = Expression.Lambda(predicate, (ParameterExpression)argContext);
            return ExpressionUtil.MakeCallOnQueryable("Any", new[] { argContext.Type }, context, lambda);
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
            if (args.Length == 1)
            {
                var predicate = args.First();
                predicate = ConvertTypeIfWeCan(methodName, predicate, typeof(bool));
                allArgs.Add(Expression.Lambda(predicate, (ParameterExpression)argContext));
            }

            return ExpressionUtil.MakeCallOnQueryable(actualMethodName, new[] { argContext.Type }, allArgs.ToArray());
        }

        private static Expression MakeLastMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, "Last");
        }

        private static Expression MakeTakeMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var amount = args.First();
            amount = ConvertTypeIfWeCan(methodName, amount, typeof(int));

            return ExpressionUtil.MakeCallOnQueryable("Take", new[] { argContext.Type }, context, amount);
        }

        private static Expression MakeSkipMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var amount = args.First();
            amount = ConvertTypeIfWeCan(methodName, amount, typeof(int));

            return ExpressionUtil.MakeCallOnQueryable("Skip", new[] { argContext.Type }, context, amount);
        }

        private static Expression MakeOrderByMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var column = args.First();
            var lambda = Expression.Lambda(column, (ParameterExpression)argContext);

            return ExpressionUtil.MakeCallOnQueryable("OrderBy", new[] { argContext.Type, column.Type }, context, lambda);
        }

        private static Expression MakeOrderByDescMethod(Expression context, Expression argContext, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var column = args.First();
            var lambda = Expression.Lambda(column, (ParameterExpression)argContext);

            return ExpressionUtil.MakeCallOnQueryable("OrderByDescending", new[] { argContext.Type, column.Type }, context, lambda);
        }

        private static Expression MakeStringContainsMethod(Expression context, string methodName, Expression[] args) =>
            MakeStringMethod(string.Empty.Contains, context, methodName, args);
        private static Expression MakeStringStartsWithMethod(Expression context, string methodName, Expression[] args) =>
            MakeStringMethod(string.Empty.StartsWith, context, methodName, args);
        private static Expression MakeStringEndsWithMethod(Expression context, string methodName, Expression[] args) =>
            MakeStringMethod(string.Empty.EndsWith, context, methodName, args);

        private static Expression MakeStringToLowerMethod(Expression context, string methodName, Expression[] args) =>
            MakeStringMethod(string.Empty.ToLower, context, methodName, args);
        private static Expression MakeStringToUpperMethod(Expression context, string methodName, Expression[] args) =>
            MakeStringMethod(string.Empty.ToUpper, context, methodName, args);

        private static Expression MakeStringMethod(Func<string> method, Expression context, string methodName, Expression[] args)
        {
            ExpectArgsCount(0, args, methodName);
            return Expression.Call(context, method.Method);
        }

        private static Expression MakeStringMethod(Func<string, bool> method, Expression context, string methodName, Expression[] args)
        {
            ExpectArgsCount(1, args, methodName);
            var predicate = args.First();
            predicate = ConvertTypeIfWeCan(methodName, predicate, typeof(string));
            return Expression.Call(context, method.Method, predicate);
        }

        private static Expression GetContextFromEnumerable(Expression context)
        {
            if (context.Type.IsEnumerableOrArray())
            {
                Type type = context.Type.GetGenericArguments()[0];
                return Expression.Parameter(type, $"p_{type.Name}");
            }
            var t = context.Type.GetEnumerableOrArrayType();
            if (t != null)
                return Expression.Parameter(t, $"p_{t.Name}");
            return context;
        }

        private static void ExpectArgsCount(int count, Expression[] args, string method)
        {
            if (args.Length != count)
                throw new EntityGraphQLCompilerException($"Method '{method}' expects {count} argument(s) but {args.Length} were supplied");
        }

        private static void ExpectArgsCountBetween(int low, int high, Expression[] args, string method)
        {
            if (args.Length < low || args.Length > high)
                throw new EntityGraphQLCompilerException($"Method '{method}' expects {low}-{high} argument(s) but {args.Length} were supplied");
        }

        private static Expression ConvertTypeIfWeCan(string methodName, Expression argExp, Type expected)
        {
            if (expected != argExp.Type)
            {
                try
                {
                    return Expression.Convert(argExp, expected);
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLCompilerException($"Method '{methodName}' expects parameter that evaluates to a '{expected}' result but found result type '{argExp.Type}'", ex);
                }
            }
            return argExp;
        }
    }
}
