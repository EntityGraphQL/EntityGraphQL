using System;
using System.Runtime.CompilerServices;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Field extension holding a cap on how many times a single field may be aliased in one operation.
/// Read by the query limits validator. Set via <see cref="QueryLimitsExtensions.SetMaxAliases"/>.
/// </summary>
public sealed class FieldAliasLimitExtension : BaseFieldExtension
{
    public FieldAliasLimitExtension(int maxAliases)
    {
        MaxAliases = maxAliases;
    }

    public int MaxAliases { get; }
}

/// <summary>
/// Fluent helpers for configuring per-field complexity overrides and alias limits.
/// Schema-wide limits are configured via <see cref="ExecutionOptions"/>.
/// </summary>
public static class QueryLimitsExtensions
{
    // lets the validator skip its walk entirely when no field in the schema has an alias limit.
    // weak keys so schemas built per-test/per-request are not kept alive
    private static readonly ConditionalWeakTable<ISchemaProvider, object> schemasWithFieldAliasLimits = new();
    private static readonly object marker = new();

    /// <summary>
    /// Cap how many times this field may appear as an aliased selection in a single operation
    /// (<c>{ a: totalPeople b: totalPeople }</c> is 2). Enforced pre-execution, independently of and in
    /// addition to <see cref="ExecutionOptions.MaxFieldAliases"/> — use it to lock down the few expensive
    /// fields a batched-alias attack would target while leaving cheap fields alone.
    /// Selections without an alias are not counted. Calling again replaces the previous limit.
    /// </summary>
    /// <param name="field">The field to limit.</param>
    /// <param name="maxAliases">Maximum aliased selections of this field per operation. 0 forbids aliasing it.</param>
    public static IField SetMaxAliases(this IField field, int maxAliases)
    {
        if (maxAliases < 0)
            throw new ArgumentOutOfRangeException(nameof(maxAliases), "Alias limit must not be negative");

        for (var i = field.Extensions.Count - 1; i >= 0; i--)
        {
            if (field.Extensions[i] is FieldAliasLimitExtension)
                field.Extensions.RemoveAt(i);
        }
        field.AddExtension(new FieldAliasLimitExtension(maxAliases));
        schemasWithFieldAliasLimits.AddOrUpdate(field.Schema, marker);
        return field;
    }

    internal static bool SchemaHasFieldAliasLimits(ISchemaProvider schema) => schemasWithFieldAliasLimits.TryGetValue(schema, out _);

    internal static int? TryGetMaxAliases(IField field)
    {
        foreach (var ext in field.Extensions)
        {
            if (ext is FieldAliasLimitExtension limit)
                return limit.MaxAliases;
        }
        return null;
    }

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
        ArgumentNullException.ThrowIfNull(calculator);
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
