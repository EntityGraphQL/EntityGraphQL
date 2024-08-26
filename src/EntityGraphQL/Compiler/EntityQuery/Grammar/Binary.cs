using System;
using System.Globalization;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

internal sealed class Binary(ExpressionType op, IExpression left, IExpression right) : IExpression
{
    public Type Type => throw new NotImplementedException();

    public ExpressionType Op { get; } = op;
    public IExpression Left { get; } = left;
    public IExpression Right { get; } = right;

    public Expression Compile(Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        var left = Left.Compile(context, schema, requestContext, methodProvider);
        var right = Right.Compile(context, schema, requestContext, methodProvider);
        if (left.Type != right.Type)
        {
            // if (op == ExpressionType.Equal || op == ExpressionType.NotEqual)
            // {
            //     var result = DoObjectComparisonOnDifferentTypes(op, left, right);

            //     if (result != null)
            //         return result;
            // }
            return ConvertLeftOrRight(Op, left, right);
        }
        return Expression.MakeBinary(Op, left, right);
    }

    private static BinaryExpression ConvertLeftOrRight(ExpressionType op, Expression left, Expression right)
    {
        if (left.Type.IsNullableType() && !right.Type.IsNullableType())
            right = Expression.Convert(right, right.Type.GetNullableType());
        else if (right.Type.IsNullableType() && !left.Type.IsNullableType())
            left = Expression.Convert(left, left.Type.GetNullableType());
        else if (left.Type == typeof(int) && (right.Type == typeof(uint) || right.Type == typeof(short) || right.Type == typeof(long) || right.Type == typeof(ushort) || right.Type == typeof(ulong)))
            right = Expression.Convert(right, left.Type);
        else if (left.Type == typeof(uint) && (right.Type == typeof(int) || right.Type == typeof(short) || right.Type == typeof(long) || right.Type == typeof(ushort) || right.Type == typeof(ulong)))
            left = Expression.Convert(left, right.Type);

        if (left.Type != right.Type)
        {
            if (left.Type.IsEnum && right.Type.IsEnum)
                throw new EntityGraphQLCompilerException($"Cannot compare enums of different types '{left.Type.Name}' and '{right.Type.Name}'");
            if (left.Type == typeof(Guid) || left.Type == typeof(Guid?) && right.Type == typeof(string))
                right = ConvertToGuid(right);
            else if (right.Type == typeof(Guid) || right.Type == typeof(Guid?) && left.Type == typeof(string))
                left = ConvertToGuid(left);
            else if (left.Type == typeof(DateTime) || left.Type == typeof(DateTime?) && right.Type == typeof(string))
                right = ConvertToDateTime(right);
            else if (right.Type == typeof(DateTime) || right.Type == typeof(DateTime?) && left.Type == typeof(string))
                left = ConvertToDateTime(left);
            else if (left.Type == typeof(DateTimeOffset) || left.Type == typeof(DateTimeOffset?) && right.Type == typeof(string))
                right = ConvertToDateTimeOffset(right);
            else if (right.Type == typeof(DateTimeOffset) || right.Type == typeof(DateTimeOffset?) && left.Type == typeof(string))
                left = ConvertToDateTimeOffset(left);
            else if (left.Type.IsEnum && right.Type == typeof(string))
                right = ConvertToEnum(right, left.Type);
            else if (right.Type.IsEnum && left.Type == typeof(string))
                left = ConvertToEnum(left, right.Type);
            // convert ints "up" to float/decimal
            else if (
                (left.Type == typeof(int) || left.Type == typeof(uint) || left.Type == typeof(short) || left.Type == typeof(ushort) || left.Type == typeof(long) || left.Type == typeof(ulong))
                && (right.Type == typeof(float) || right.Type == typeof(double) || right.Type == typeof(decimal))
            )
                left = Expression.Convert(left, right.Type);
            else if (
                (right.Type == typeof(int) || right.Type == typeof(uint) || right.Type == typeof(short) || right.Type == typeof(ushort) || right.Type == typeof(long) || right.Type == typeof(ulong))
                && (left.Type == typeof(float) || left.Type == typeof(double) || left.Type == typeof(decimal))
            )
                right = Expression.Convert(right, left.Type);
            else // default try to make types match
                left = Expression.Convert(left, right.Type);
        }

        if (left.Type.IsNullableType() && !right.Type.IsNullableType())
            right = Expression.Convert(right, left.Type);
        else if (right.Type.IsNullableType() && !left.Type.IsNullableType())
            left = Expression.Convert(left, right.Type);

        return Expression.MakeBinary(op, left, right);
    }

    private static MethodCallExpression ConvertToDateTimeOffset(Expression expression)
    {
        return Expression.Call(typeof(DateTimeOffset), nameof(DateTimeOffset.Parse), null, expression, Expression.Constant(CultureInfo.InvariantCulture));
    }

    private static MethodCallExpression ConvertToDateTime(Expression expression)
    {
        return Expression.Call(typeof(DateTime), nameof(DateTime.Parse), null, expression, Expression.Constant(CultureInfo.InvariantCulture));
    }

    private static MethodCallExpression ConvertToGuid(Expression expression)
    {
        return Expression.Call(typeof(Guid), nameof(Guid.Parse), null, Expression.Call(expression, typeof(object).GetMethod(nameof(ToString))!));
    }

    private static UnaryExpression ConvertToEnum(Expression expression, Type enumType)
    {
        return Expression.Convert(
            Expression.Call(typeof(Enum), nameof(Enum.Parse), null, Expression.Constant(enumType), Expression.Call(expression, typeof(object).GetMethod(nameof(ToString))!)),
            enumType
        );
    }

    // private static Expression? DoObjectComparisonOnDifferentTypes(ExpressionType op, Expression left, Expression right)
    // {
    //     var convertedToSameTypes = false;

    //     // leftGuid == 'asdasd' == null ? (Guid) null : new Guid('asdasdas'.ToString())
    //     // leftGuid == null
    //     if (left.Type == typeof(Guid) && right.Type != typeof(Guid))
    //     {
    //         right = ConvertToGuid(right);
    //         convertedToSameTypes = true;
    //     }
    //     else if (right.Type == typeof(Guid) && left.Type != typeof(Guid))
    //     {
    //         left = ConvertToGuid(left);
    //         convertedToSameTypes = true;
    //     }

    //     var result = convertedToSameTypes ? (Expression)Expression.MakeBinary(op, left, right) : null;
    //     return result;
    // }
}
