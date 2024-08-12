using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.EntityQuery;

/// <summary>
/// The default method provider for Ling Queries. Implements the useful Linq functions for
/// querying and filtering your data requests.
/// </summary>
public class DefaultMethodProvider : IMethodProvider
{
    // Map of the method names and a function that makes the Expression.Call
    private readonly Dictionary<Func<Type, bool>, Dictionary<string, Func<Expression, Expression, string, Expression[], Expression>>> supportedMethods =
        new()
        {
            {
                (Type t) => t == typeof(string),
                new(StringComparer.OrdinalIgnoreCase)
                {
                    { "contains", MakeStringContainsMethod },
                    { "startsWith", MakeStringStartsWithMethod },
                    { "endsWith", MakeStringEndsWithMethod },
                    { "toLower", MakeStringToLowerMethod },
                    { "toUpper", MakeStringToUpperMethod },
                }
            },
            {
                (Type t) => t.IsEnumerableOrArray(),
                new(StringComparer.OrdinalIgnoreCase)
                {
                    { "where", MakeWhereMethod },
                    { "filter", MakeWhereMethod },
                    { "first", MakeFirstMethod },
                    { "firstOrDefault", MakeFirstOrDefaultMethod },
                    { "last", MakeLastMethod },
                    { "lastOrDefault", MakeLastOrDefaultMethod },
                    { "take", MakeTakeMethod },
                    { "skip", MakeSkipMethod },
                    { "count", MakeCountMethod },
                    { "any", MakeAnyMethod },
                    { "orderby", MakeOrderByMethod },
                    { "orderByDesc", MakeOrderByDescMethod },
                }
            },
            {
                (Type t) =>
                    t == typeof(string)
                    || t == typeof(long)
                    || t == typeof(int)
                    || t == typeof(short)
                    || t == typeof(byte)
                    || t == typeof(double)
                    || t == typeof(float)
                    || t == typeof(decimal)
                    || t == typeof(uint)
                    || t == typeof(ulong)
                    || t == typeof(ushort)
                    || t == typeof(sbyte)
                    || t == typeof(char)
                    || t == typeof(DateTime)
                    || t == typeof(Guid)
                    || t == typeof(DateTimeOffset)
#if NET5_0_OR_GREATER
                    || t == typeof(DateOnly)
                    || t == typeof(TimeOnly)
#endif
                ,
                new(StringComparer.OrdinalIgnoreCase) { { "isAny", MakeIsAnyMethod }, }
            },
        };

    public bool EntityTypeHasMethod(Type context, string methodName)
    {
        foreach (var item in supportedMethods)
        {
            if (item.Key(context) && item.Value.ContainsKey(methodName))
                return true;
        }

        return false;
    }

    public Expression GetMethodContext(Expression context, string methodName)
    {
        // some methods have a context of the element type in the list, other is just the original context
        // need some way for the method compiler to tells us that
        //  return _supportedMethods[methodName](context);
        return GetContextFromEnumerable(context);
    }

    public Expression MakeCall(Expression context, Expression argContext, string methodName, IEnumerable<Expression>? args, Type type)
    {
        foreach (var item in supportedMethods)
        {
            if (item.Key(type) && item.Value.TryGetValue(methodName, out var func))
            {
                return func(context, argContext, methodName, args != null ? args.ToArray() : []);
            }
        }

        throw new EntityGraphQLCompilerException($"Unsupported method {methodName}");
    }

    private static Expression MakeWhereMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var predicate = args.First();
        predicate = ConvertTypeIfWeCan(methodName, predicate, typeof(bool));
        var lambda = Expression.Lambda(predicate, (ParameterExpression)argContext);
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Where), [argContext.Type], context, lambda);
    }

    private static Expression MakeAnyMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var predicate = args.First();
        predicate = ConvertTypeIfWeCan(methodName, predicate, typeof(bool));
        var lambda = Expression.Lambda(predicate, (ParameterExpression)argContext);
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Any), [argContext.Type], context, lambda);
    }

    private static Expression MakeFirstMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, nameof(Enumerable.First));
    }

    private static Expression MakeFirstOrDefaultMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, nameof(Enumerable.FirstOrDefault));
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

        return ExpressionUtil.MakeCallOnQueryable(actualMethodName, [argContext.Type], allArgs.ToArray());
    }

    private static Expression MakeLastMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, nameof(Enumerable.Last));
    }

    private static Expression MakeLastOrDefaultMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        return MakeOptionalFilterArgumentCall(context, argContext, methodName, args, nameof(Enumerable.LastOrDefault));
    }

    private static Expression MakeTakeMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var amount = args.First();
        amount = ConvertTypeIfWeCan(methodName, amount, typeof(int));

        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Take), [argContext.Type], context, amount);
    }

    private static Expression MakeSkipMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var amount = args.First();
        amount = ConvertTypeIfWeCan(methodName, amount, typeof(int));

        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Skip), [argContext.Type], context, amount);
    }

    private static Expression MakeOrderByMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var column = args.First();
        var lambda = Expression.Lambda(column, (ParameterExpression)argContext);

        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.OrderBy), [argContext.Type, column.Type], context, lambda);
    }

    private static Expression MakeOrderByDescMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var column = args.First();
        var lambda = Expression.Lambda(column, (ParameterExpression)argContext);

        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.OrderByDescending), [argContext.Type, column.Type], context, lambda);
    }

    private static Expression MakeIsAnyMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var array = args.First();
        var arrayType = array.Type.GetEnumerableOrArrayType() ?? throw new EntityGraphQLCompilerException("Could not get element type from enumerable/array");

        return ExpressionUtil.MakeCallOnQueryable(nameof(Enumerable.Contains), [arrayType], array, context);
    }

    private static Expression MakeStringContainsMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeStringMethod(string.Empty.Contains, context, methodName, args);

    private static Expression MakeStringStartsWithMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeStringMethod(string.Empty.StartsWith, context, methodName, args);

    private static Expression MakeStringEndsWithMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeStringMethod(string.Empty.EndsWith, context, methodName, args);

    private static Expression MakeStringToLowerMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeStringMethod(string.Empty.ToLower, context, methodName, args);

    private static Expression MakeStringToUpperMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeStringMethod(string.Empty.ToUpper, context, methodName, args);

    private static MethodCallExpression MakeStringMethod(Func<string> method, Expression context, string methodName, Expression[] args)
    {
        ExpectArgsCount(0, args, methodName);
        return Expression.Call(context, method.Method);
    }

    private static MethodCallExpression MakeStringMethod(Func<string, bool> method, Expression context, string methodName, Expression[] args)
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
            Type type = context.Type.GetEnumerableOrArrayType() ?? throw new EntityGraphQLCompilerException("Could not get element type from enumerable/array");
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
