using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.EntityQuery;

/// <summary>
/// Contains all the default in-built method implementations for the EqlMethodProvider.
/// These methods handle the built-in filter language operations like contains, where, orderBy, etc.
/// </summary>
internal static class DefaultMethodImplementations
{
    #region String Method Implementations

    internal static Expression MakeStringContainsMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var predicate = ConvertTypeIfWeCan(methodName, args[0], typeof(string));
        return Expression.Call(context, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, predicate);
    }

    internal static Expression MakeStringStartsWithMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var predicate = ConvertTypeIfWeCan(methodName, args[0], typeof(string));
        return Expression.Call(context, typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!, predicate);
    }

    internal static Expression MakeStringEndsWithMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var predicate = ConvertTypeIfWeCan(methodName, args[0], typeof(string));
        return Expression.Call(context, typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!, predicate);
    }

    internal static Expression MakeStringToLowerMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(0, args, methodName);
        return Expression.Call(context, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
    }

    internal static Expression MakeStringToUpperMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(0, args, methodName);
        return Expression.Call(context, typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!);
    }

    #endregion

    #region Enumerable Method Implementations

    internal static Expression MakeWhereMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var predicate = ConvertTypeIfWeCan(methodName, args[0], typeof(bool));
        var lambda = Expression.Lambda(predicate, (ParameterExpression)argContext);
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Where), [argContext.Type], context, lambda);
    }

    internal static Expression MakeAnyMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var predicate = ConvertTypeIfWeCan(methodName, args[0], typeof(bool));
        var lambda = Expression.Lambda(predicate, (ParameterExpression)argContext);
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Any), [argContext.Type], context, lambda);
    }

    internal static Expression MakeFirstMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeOptionalFilterArgumentCall(context, argContext, methodName, args, nameof(Enumerable.First));

    internal static Expression MakeFirstOrDefaultMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeOptionalFilterArgumentCall(context, argContext, methodName, args, nameof(Enumerable.FirstOrDefault));

    internal static Expression MakeLastMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeOptionalFilterArgumentCall(context, argContext, methodName, args, nameof(Enumerable.Last));

    internal static Expression MakeLastOrDefaultMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeOptionalFilterArgumentCall(context, argContext, methodName, args, nameof(Enumerable.LastOrDefault));

    internal static Expression MakeCountMethod(Expression context, Expression argContext, string methodName, Expression[] args) =>
        MakeOptionalFilterArgumentCall(context, argContext, methodName, args, "Count");

    private static Expression MakeOptionalFilterArgumentCall(Expression context, Expression argContext, string methodName, Expression[] args, string actualMethodName)
    {
        ExpectArgsCountBetween(0, 1, args, methodName);

        var allArgs = new List<Expression> { context };
        if (args.Length == 1)
        {
            var predicate = ConvertTypeIfWeCan(methodName, args[0], typeof(bool));
            allArgs.Add(Expression.Lambda(predicate, (ParameterExpression)argContext));
        }

        return ExpressionUtil.MakeCallOnQueryable(actualMethodName, [argContext.Type], allArgs.ToArray());
    }

    internal static Expression MakeTakeMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var amount = ConvertTypeIfWeCan(methodName, args[0], typeof(int));
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Take), [argContext.Type], context, amount);
    }

    internal static Expression MakeSkipMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var amount = ConvertTypeIfWeCan(methodName, args[0], typeof(int));
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Skip), [argContext.Type], context, amount);
    }

    internal static Expression MakeOrderByMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var column = args[0];
        var lambda = Expression.Lambda(column, (ParameterExpression)argContext);
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.OrderBy), [argContext.Type, column.Type], context, lambda);
    }

    internal static Expression MakeOrderByDescMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var column = args[0];
        var lambda = Expression.Lambda(column, (ParameterExpression)argContext);
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.OrderByDescending), [argContext.Type, column.Type], context, lambda);
    }

    internal static Expression MakeSelectManyMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var selector = args[0];
        var lambda = Expression.Lambda(selector, (ParameterExpression)argContext);

        // Get the result type of the selector - should be an enumerable
        var selectorResultType = selector.Type;
        var elementType =
            selectorResultType.GetEnumerableOrArrayType()
            ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"selectMany selector must return an enumerable type, but got '{selectorResultType}'");

        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.SelectMany), [argContext.Type, elementType], context, lambda);
    }

    #endregion

    #region Special Method Implementations

    internal static Expression MakeIsAnyMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var array = args[0];
        var arrayType = array.Type.GetEnumerableOrArrayType() ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, "Could not get element type from enumerable/array");
        var isQueryable = typeof(IQueryable).IsAssignableFrom(array.Type);

        if (context.Type.IsNullableType())
        {
            var call = isQueryable
                ? ExpressionUtil.MakeCallOnQueryable(nameof(Enumerable.Contains), [arrayType], array, Expression.Convert(context, arrayType))
                : ExpressionUtil.MakeCallOnEnumerable(nameof(Enumerable.Contains), [arrayType], array, Expression.Convert(context, arrayType));
            return Expression.Condition(Expression.Equal(context, Expression.Constant(null, context.Type)), Expression.Constant(false), call);
        }

        return isQueryable
            ? ExpressionUtil.MakeCallOnQueryable(nameof(Enumerable.Contains), [arrayType], array, Expression.Convert(context, arrayType))
            : ExpressionUtil.MakeCallOnEnumerable(nameof(Enumerable.Contains), [arrayType], array, Expression.Convert(context, arrayType));
    }

    internal static Expression MakeAllMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        ExpectArgsCount(1, args, methodName);
        var condition = args[0];
        var lambda = Expression.Lambda(condition, (ParameterExpression)argContext);
        return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.All), [argContext.Type], context, lambda);
    }

    internal static Expression MakeSumMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        if (args.Length == 0)
        {
            // Sum() - direct sum of numeric elements
            return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Sum), [argContext.Type], context);
        }
        else if (args.Length == 1)
        {
            // Sum(selector) - sum with projection
            var selector = args[0];
            var lambda = Expression.Lambda(selector, (ParameterExpression)argContext);
            return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Sum), [argContext.Type], context, lambda);
        }
        else
        {
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{methodName}' expects 0 or 1 arguments but {args.Length} were supplied");
        }
    }

    internal static Expression MakeMinMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        if (args.Length == 0)
        {
            return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Min), [argContext.Type], context);
        }
        else if (args.Length == 1)
        {
            var selector = args[0];
            var lambda = Expression.Lambda(selector, (ParameterExpression)argContext);
            return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Min), [argContext.Type], context, lambda);
        }
        else
        {
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{methodName}' expects 0 or 1 arguments but {args.Length} were supplied");
        }
    }

    internal static Expression MakeMaxMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        if (args.Length == 0)
        {
            return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Max), [argContext.Type], context);
        }
        else if (args.Length == 1)
        {
            var selector = args[0];
            var lambda = Expression.Lambda(selector, (ParameterExpression)argContext);
            return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Max), [argContext.Type], context, lambda);
        }
        else
        {
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{methodName}' expects 0 or 1 arguments but {args.Length} were supplied");
        }
    }

    internal static Expression MakeAverageMethod(Expression context, Expression argContext, string methodName, Expression[] args)
    {
        if (args.Length == 0)
        {
            return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Average), [argContext.Type], context);
        }
        else if (args.Length == 1)
        {
            var selector = args[0];
            var lambda = Expression.Lambda(selector, (ParameterExpression)argContext);
            return ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Average), [argContext.Type], context, lambda);
        }
        else
        {
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{methodName}' expects 0 or 1 arguments but {args.Length} were supplied");
        }
    }

    internal static Expression GetContextFromEnumerable(Expression context)
    {
        if (context.Type.IsEnumerableOrArray())
        {
            Type type = context.Type.GetEnumerableOrArrayType() ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, "Could not get element type from enumerable/array");
            return Expression.Parameter(type, $"p_{type.Name}");
        }
        var t = context.Type.GetEnumerableOrArrayType();
        return t != null ? Expression.Parameter(t, $"p_{t.Name}") : context;
    }

    #endregion

    #region Helper Methods

    private static void ExpectArgsCount(int count, Expression[] args, string method)
    {
        if (args.Length != count)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{method}' expects {count} argument(s) but {args.Length} were supplied");
    }

    private static void ExpectArgsCountBetween(int low, int high, Expression[] args, string method)
    {
        if (args.Length < low || args.Length > high)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{method}' expects {low}-{high} argument(s) but {args.Length} were supplied");
    }

    private static Expression ConvertTypeIfWeCan(string methodName, Expression argExp, Type expected)
    {
        if (expected == argExp.Type)
            return argExp;

        try
        {
            return Expression.Convert(argExp, expected);
        }
        catch (Exception ex)
        {
            throw new EntityGraphQLException(
                GraphQLErrorCategory.DocumentError,
                $"Method '{methodName}' expects parameter that evaluates to a '{expected}' result but found result type '{argExp.Type}'",
                null,
                [methodName],
                ex
            );
        }
    }

    #endregion
}
