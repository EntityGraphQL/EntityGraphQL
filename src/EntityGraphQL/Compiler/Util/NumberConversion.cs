using System;
using System.Globalization;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// double/float to decimal as the number reads (1.2 -> 1.2m). Since .NET 11 the decimal conversions keep the
/// binary value's full precision (1.2 -> 1.1999999999999999555910790150m); before, they rounded to 15 significant
/// digits. A GraphQL Float or filter value for a decimal argument or field means the number as written.
/// </summary>
internal static class NumberConversion
{
    public static decimal ToDecimal(double value) => decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

    public static decimal ToDecimal(float value) => decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

    public static decimal? ToDecimal(double? value) => value.HasValue ? ToDecimal(value.Value) : null;

    public static decimal? ToDecimal(float? value) => value.HasValue ? ToDecimal(value.Value) : null;

    /// <summary>The value as decimal if it is a double or float, otherwise null</summary>
    public static decimal? TryToDecimal(object value) =>
        value switch
        {
            double d => ToDecimal(d),
            float f => ToDecimal(f),
            _ => null,
        };

    /// <summary>Expression.Convert(expression, type), except double/float to decimal converts as the number reads</summary>
    public static Expression Convert(Expression expression, Type type)
    {
        var from = Nullable.GetUnderlyingType(expression.Type) ?? expression.Type;
        if ((Nullable.GetUnderlyingType(type) ?? type) != typeof(decimal) || (from != typeof(double) && from != typeof(float)))
            return Expression.Convert(expression, type);
        var call = Expression.Call(typeof(NumberConversion), nameof(ToDecimal), null, expression);
        return call.Type == type ? call : Expression.Convert(call, type);
    }
}
