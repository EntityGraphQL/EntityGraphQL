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
                // Align int/uint/short/long widths if mixed
                else if (
                    left.Type == typeof(int)
                    && (right.Type == typeof(uint) || right.Type == typeof(short) || right.Type == typeof(long) || right.Type == typeof(ushort) || right.Type == typeof(ulong))
                )
                    right = Expression.Convert(right, left.Type);
                else if (
                    left.Type == typeof(uint)
                    && (right.Type == typeof(int) || right.Type == typeof(short) || right.Type == typeof(long) || right.Type == typeof(ushort) || right.Type == typeof(ulong))
                )
                    left = Expression.Convert(left, right.Type);
                // convert ints "up" to float/decimal
                else if (
                    (left.Type == typeof(int) || left.Type == typeof(uint) || left.Type == typeof(short) || left.Type == typeof(ushort) || left.Type == typeof(long) || left.Type == typeof(ulong))
                    && (right.Type == typeof(float) || right.Type == typeof(double) || right.Type == typeof(decimal))
                )
                    left = Expression.Convert(left, right.Type);
                else if (
                    (
                        right.Type == typeof(int)
                        || right.Type == typeof(uint)
                        || right.Type == typeof(short)
                        || right.Type == typeof(ushort)
                        || right.Type == typeof(long)
                        || right.Type == typeof(ulong)
                    ) && (left.Type == typeof(float) || left.Type == typeof(double) || left.Type == typeof(decimal))
                )
                    right = Expression.Convert(right, left.Type);
                else if (left.Type != right.Type) // default try to make types match
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
}
