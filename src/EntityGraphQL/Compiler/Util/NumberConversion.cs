using System;
using System.Globalization;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// double/float to decimal rounded to 15 (float: 7) significant digits (1.2 -> 1.2m), as .NET did before 11. Since
/// .NET 11 the decimal conversions keep the binary value's full precision (1.2 -> 1.1999999999999999555910790150m).
/// This keeps every target converting as earlier versions of EntityGraphQL did: the same result for any value of up
/// to 15 significant digits. For 16-17 digits whose 16th is a 5, the old runtime's approximate rounding could be one
/// off in the 15th digit; this rounds the exact value.
/// </summary>
internal static class NumberConversion
{
    // .NET before 11: double -> decimal kept at most 15 significant digits, float -> decimal at most 7
    public static decimal ToDecimal(double value) => decimal.Parse(value.ToString("G15", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

    public static decimal ToDecimal(float value) => decimal.Parse(value.ToString("G7", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);

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

    /// <summary>Expression.Convert(expression, type), except double/float to decimal converts as before .NET 11</summary>
    public static Expression Convert(Expression expression, Type type)
    {
        var from = Nullable.GetUnderlyingType(expression.Type) ?? expression.Type;
        if ((Nullable.GetUnderlyingType(type) ?? type) != typeof(decimal) || (from != typeof(double) && from != typeof(float)))
            return Expression.Convert(expression, type);
        var call = Expression.Call(typeof(NumberConversion), nameof(ToDecimal), null, expression);
        return call.Type == type ? call : Expression.Convert(call, type);
    }
}
