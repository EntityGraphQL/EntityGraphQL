using System;
using System.Globalization;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// double/float to decimal rounded to 15 (float: 7) significant digits (1.2 -> 1.2m), as .NET did before 11. Since
/// .NET 11 the decimal conversions keep the binary value's full precision (1.2 -> 1.1999999999999999555910790150m).
/// Before .NET 11 this is the runtime's own conversion, so nothing changes there. On .NET 11 it gives the same
/// result for any value of up to 15 significant digits; for 16-17 digits whose 16th is a 5, the old runtime's
/// approximate rounding could be one off in the 15th digit, where this rounds the exact value.
/// </summary>
internal static class NumberConversion
{
    // the running runtime, not the target framework - a net10.0 build can be rolled forward onto .NET 11
    private static readonly bool RuntimeRounds = Environment.Version.Major < 11;

    public static decimal ToDecimal(double value) => RuntimeRounds ? (decimal)value : Round(value);

    public static decimal ToDecimal(float value) => RuntimeRounds ? (decimal)value : Round(value);

    // NaN and infinity go to the cast, which throws the OverflowException it always has
    internal static decimal Round(double value) =>
        double.IsFinite(value) ? decimal.Parse(value.ToString("G15", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture) : (decimal)value;

    internal static decimal Round(float value) =>
        float.IsFinite(value) ? decimal.Parse(value.ToString("G7", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture) : (decimal)value;

    /// <summary>The value as decimal if it is a double or float, otherwise null</summary>
    public static decimal? TryToDecimal(object value) =>
        value switch
        {
            double d => ToDecimal(d),
            float f => ToDecimal(f),
            _ => null,
        };

    /// <summary>
    /// Expression.Convert(expression, type), except a double/float constant is converted to decimal here, as before
    /// .NET 11. Anything else stays a plain convert so a query provider (EF) can translate it to a cast - a method
    /// call it could not. In memory on .NET 11 that convert keeps the double's full precision.
    /// </summary>
    public static Expression Convert(Expression expression, Type type)
    {
        if ((Nullable.GetUnderlyingType(type) ?? type) == typeof(decimal) && expression is ConstantExpression { Value: not null } constant && TryToDecimal(constant.Value) is { } asDecimal)
            return Expression.Constant(asDecimal, type);
        return Expression.Convert(expression, type);
    }
}
