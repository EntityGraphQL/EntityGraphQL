using System;
using System.Collections.Generic;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Field extension that stores a custom complexity score for a field. Either a fixed integer or a
/// calculator that can inspect the field's arguments and its children's cost.
/// Read by <see cref="DefaultQueryComplexityAnalyzer"/> when costing a query.
/// </summary>
public sealed class FieldComplexityExtension : BaseFieldExtension
{
    public int? FixedCost { get; }
    public Func<FieldComplexityContext, int>? Calculator { get; }

    public FieldComplexityExtension(int fixedCost)
    {
        FixedCost = fixedCost;
    }

    public FieldComplexityExtension(Func<FieldComplexityContext, int> calculator)
    {
        Calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }
}

/// <summary>
/// Context passed to a <see cref="FieldComplexityExtension"/> calculator. Exposes the field's arguments and
/// the sum of its children's cost so the calculator can compute list-multiplier style cost models without
/// the analyzer having to guess arg names.
/// </summary>
public readonly struct FieldComplexityContext
{
    public FieldComplexityContext(IReadOnlyDictionary<string, object?> arguments, int childComplexity, object? prebuiltArgs = null)
    {
        Arguments = arguments;
        ChildComplexity = childComplexity;
        PrebuiltArgs = prebuiltArgs;
    }

    /// <summary>Arguments on this field selection, with $variable references already resolved to their real values.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }

    /// <summary>Sum of cost of every child selection. Use this to multiply a list-size argument by child cost.</summary>
    public int ChildComplexity { get; }

    /// <summary>The field's arguments pre-built as the declared args type — cast to access them typed.</summary>
    internal object? PrebuiltArgs { get; }

    /// <summary>Try to read an argument by name, returning <paramref name="defaultValue"/> if missing or not a <typeparamref name="T"/>.</summary>
    public T? Arg<T>(string name, T? defaultValue = default)
    {
        if (Arguments.TryGetValue(name, out var raw) && raw is T t)
            return t;
        return defaultValue;
    }

    /// <summary>
    /// Bind the field's arguments to a strongly-typed <typeparamref name="T"/>. The property / constructor-parameter
    /// names must match the argument names (case-sensitive, as the schema uses them). Values from inline
    /// literals bind directly; values sourced from query <c>$variables</c> are not resolved at cost-calc time
    /// and will surface as the property's default.
    ///
    /// Useful when you want <c>args.take</c> style access in a calculator without passing a sample object.
    /// </summary>
    internal T ArgsAs<T>() => (T)PrebuiltArgs!;
}

/// <summary>
/// Typed variant of <see cref="FieldComplexityContext"/> used by the strongly-typed <c>SetComplexity</c>
/// method on fields declared with an args shape. <see cref="Args"/> is already bound to <typeparamref name="TParams"/>
/// so the calculator can write <c>ctx.Args.take</c> directly.
/// </summary>
public readonly struct FieldComplexityContext<TParams>
{
    public FieldComplexityContext(TParams args, IReadOnlyDictionary<string, object?> arguments, int childComplexity)
    {
        Args = args;
        Arguments = arguments;
        ChildComplexity = childComplexity;
    }

    /// <summary>The field's arguments bound to the declared <typeparamref name="TParams"/>.</summary>
    public TParams Args { get; }

    /// <summary>Raw argument dictionary — escape hatch for anything <see cref="Args"/> doesn't expose.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }

    /// <summary>Sum of cost of every child selection — multiply by your row count for list-aware cost.</summary>
    public int ChildComplexity { get; }
}

internal static class FieldComplexityLookup
{
    public static FieldComplexityExtension? TryGet(IField field)
    {
        foreach (var ext in field.Extensions)
        {
            if (ext is FieldComplexityExtension fc)
                return fc;
        }
        return null;
    }
}
