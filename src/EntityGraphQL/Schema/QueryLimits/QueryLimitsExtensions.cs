using System;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Fluent helpers for configuring per-field complexity overrides.
/// Schema-wide limits are configured via <see cref="ExecutionOptions"/>.
/// </summary>
public static class QueryLimitsExtensions
{
    /// <summary>
    /// Set a fixed complexity score for this field. The field's cost is <paramref name="complexity"/>
    /// plus the sum of its children's cost. Used when <see cref="ExecutionOptions.MaxQueryComplexity"/> is set.
    /// </summary>
    public static IField SetComplexity(this IField field, int complexity)
    {
        RemoveExisting(field);
        field.AddExtension(new FieldComplexityExtension(complexity));
        return field;
    }

    /// <summary>
    /// Set a computed complexity score for this field. The <paramref name="calculator"/> receives the
    /// field's arguments (e.g. <c>first</c>/<c>take</c>) and the sum of its children's cost, so you can
    /// express models like <c>cost = take * (1 + childCost)</c> without relying on argument-name heuristics.
    /// The return value is the field's total cost — it is <b>not</b> added to children's cost again.
    /// </summary>
    public static IField SetComplexity(this IField field, Func<FieldComplexityContext, int> calculator)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(calculator);
#else
        if (calculator == null)
            throw new ArgumentNullException(nameof(calculator));
#endif
        RemoveExisting(field);
        field.AddExtension(new FieldComplexityExtension(calculator));
        return field;
    }

    private static void RemoveExisting(IField field)
    {
        for (var i = field.Extensions.Count - 1; i >= 0; i--)
        {
            if (field.Extensions[i] is FieldComplexityExtension)
                field.Extensions.RemoveAt(i);
        }
    }
}
