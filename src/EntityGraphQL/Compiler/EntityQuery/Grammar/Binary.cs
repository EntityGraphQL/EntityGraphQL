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

    public Expression Compile(Expression? context, EntityQueryParser parser, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        var left = Left.Compile(context, parser, schema, requestContext, methodProvider);
        var right = Right.Compile(context, parser, schema, requestContext, methodProvider);
        if (left.Type != right.Type)
        {
            return ConvertLeftOrRight(Op, left, right, parser, schema);
        }
        return Expression.MakeBinary(Op, left, right);
    }

    private static BinaryExpression ConvertLeftOrRight(ExpressionType op, Expression left, Expression right, EntityQueryParser parser, ISchemaProvider? schema)
    {
        // Defer nullable promotion and numeric width alignment until after we attempt literal parsing and specific conversions
        if (left.Type != right.Type)
        {
            // Try to use type converters for constant string expressions first
            if (schema != null && right is ConstantExpression { Value: string str } && left.Type != typeof(string))
            {
                if (schema.TryConvertCustom(str, left.Type, out var converted))
                {
                    right = Expression.Constant(converted, left.Type);
                }
            }
            else if (schema != null && left is ConstantExpression { Value: string str2 } && right.Type != typeof(string))
            {
                if (schema.TryConvertCustom(str2, right.Type, out var converted))
                {
                    left = Expression.Constant(converted, right.Type);
                }
            }

            if (left.Type.IsEnum && right.Type.IsEnum)
                throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Cannot compare enums of different types '{left.Type.Name}' and '{right.Type.Name}'");

            // If types are now equal after literal parser, skip further specific conversions
            if (left.Type != right.Type)
            {
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
                else if (left.Type == typeof(TimeSpan) || left.Type == typeof(TimeSpan?) && right.Type == typeof(string))
                    right = ConvertToTimeSpan(right);
                else if (right.Type == typeof(TimeSpan) || right.Type == typeof(TimeSpan?) && left.Type == typeof(string))
                    left = ConvertToTimeSpan(left);
#if NET8_0_OR_GREATER
                else if (left.Type == typeof(DateOnly) || left.Type == typeof(DateOnly?) && right.Type == typeof(string))
                    right = ConvertToDateOnly(right);
                else if (right.Type == typeof(DateOnly) || right.Type == typeof(DateOnly?) && left.Type == typeof(string))
                    left = ConvertToDateOnly(left);
                else if (left.Type == typeof(TimeOnly) || left.Type == typeof(TimeOnly?) && right.Type == typeof(string))
                    right = ConvertToTimeOnly(right);
                else if (right.Type == typeof(TimeOnly) || right.Type == typeof(TimeOnly?) && left.Type == typeof(string))
                    left = ConvertToTimeOnly(left);
#endif
                else if (left.Type.IsEnum && right.Type == typeof(string))
                    right = ConvertToEnum(right, left.Type);
                else if (right.Type.IsEnum && left.Type == typeof(string))
                    left = ConvertToEnum(left, right.Type);
                // Convert integral types "up" to floating-point types (float/double/decimal)
                else if (IsIntegralOrNullableIntegral(left.Type) && IsFloatingPointOrNullable(right.Type))
                    left = Expression.Convert(left, right.Type);
                else if (IsIntegralOrNullableIntegral(right.Type) && IsFloatingPointOrNullable(left.Type))
                    right = Expression.Convert(right, left.Type);
                // Align floating-point types (float/double/decimal) or integral types
                else if (!AlignFloatingPointTypes(ref left, ref right) && !AlignIntegralTypes(ref left, ref right) && left.Type != right.Type)
                    left = Expression.Convert(left, right.Type);
            }
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

    private static MethodCallExpression ConvertToTimeSpan(Expression expression)
    {
        return Expression.Call(typeof(TimeSpan), nameof(TimeSpan.Parse), null, expression, Expression.Constant(CultureInfo.InvariantCulture));
    }

#if NET8_0_OR_GREATER
    private static MethodCallExpression ConvertToDateOnly(Expression expression)
    {
        return Expression.Call(typeof(DateOnly), nameof(DateOnly.Parse), null, expression, Expression.Constant(CultureInfo.InvariantCulture));
    }

    private static MethodCallExpression ConvertToTimeOnly(Expression expression)
    {
        return Expression.Call(typeof(TimeOnly), nameof(TimeOnly.Parse), null, expression, Expression.Constant(CultureInfo.InvariantCulture));
    }
#endif

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

    /// <summary>
    /// Aligns integral numeric types between left and right expressions, including nullable types.
    /// </summary>
    private static bool AlignIntegralTypes(ref Expression left, ref Expression right) => AlignNumericTypes(ref left, ref right, IsIntegralType);

    /// <summary>
    /// Aligns floating-point numeric types (float, double, decimal) between left and right expressions, including nullable types.
    /// </summary>
    private static bool AlignFloatingPointTypes(ref Expression left, ref Expression right) => AlignNumericTypes(ref left, ref right, IsFloatingPointType);

    /// <summary>
    /// Aligns numeric types between left and right expressions, including nullable types.
    /// Prioritizes non-constant expressions (e.g., field access) over constants to avoid database column casts.
    /// Returns true if alignment was performed.
    /// </summary>
    /// <param name="left">The left expression (may be modified)</param>
    /// <param name="right">The right expression (may be modified)</param>
    /// <param name="typeChecker">Function to check if a type belongs to the numeric category being aligned</param>
    private static bool AlignNumericTypes(ref Expression left, ref Expression right, Func<Type, bool> typeChecker)
    {
        // Get the underlying types (unwrap nullable if needed)
        var leftType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
        var rightType = Nullable.GetUnderlyingType(right.Type) ?? right.Type;

        // Check if both types belong to the same numeric category
        if (!typeChecker(leftType) || !typeChecker(rightType))
            return false;

        // Determine which side to prioritize - prefer non-constant side (field access) over constant (literal).
        // This avoids casting database columns which can prevent index usage.
        // Truth table for prioritizeLeft:
        //   left=constant, right=constant     -> true  (default to left)
        //   left=constant, right=non-constant -> false (prioritize right, the field)
        //   left=non-constant, right=constant -> true  (prioritize left, the field)
        //   left=non-constant, right=non-constant -> true  (default to left)
        var leftIsConstant = left is ConstantExpression;
        var rightIsConstant = right is ConstantExpression;
        var prioritizeLeft = !leftIsConstant || rightIsConstant;

        // Convert to match the prioritized side's type
        if (leftType != rightType)
        {
            if (prioritizeLeft)
                right = Expression.Convert(right, left.Type);
            else
                left = Expression.Convert(left, right.Type);
            return true;
        }

        // Types are the same underlying type but might differ in nullability
        if (left.Type != right.Type)
        {
            if (prioritizeLeft)
                right = Expression.Convert(right, left.Type);
            else
                left = Expression.Convert(left, right.Type);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a type is an integral numeric type (int, uint, short, ushort, long, ulong, byte, sbyte)
    /// </summary>
    private static bool IsIntegralType(Type type)
    {
        return type == typeof(int)
            || type == typeof(uint)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(byte)
            || type == typeof(sbyte);
    }

    /// <summary>
    /// Checks if a type is a floating-point type (float, double, decimal)
    /// </summary>
    private static bool IsFloatingPointType(Type type)
    {
        return type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    }

    /// <summary>
    /// Checks if a type is an integral type or nullable integral type
    /// </summary>
    private static bool IsIntegralOrNullableIntegral(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return IsIntegralType(underlyingType);
    }

    /// <summary>
    /// Checks if a type is a floating-point type (float, double, decimal) or nullable version
    /// </summary>
    private static bool IsFloatingPointOrNullable(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType == typeof(float) || underlyingType == typeof(double) || underlyingType == typeof(decimal);
    }
}
