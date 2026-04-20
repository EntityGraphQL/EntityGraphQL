using System;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Sets a fixed complexity cost on a field or mutation method. Equivalent to calling
/// <c>field.SetComplexity(cost)</c> fluently after schema construction.
///
/// Applies to query fields (properties / methods on the context type) and mutation methods
/// decorated with <c>[GraphQLMutation]</c>.
/// <code>
/// [FieldComplexity(50)]
/// public Report GenerateReport() { ... }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
public sealed class FieldComplexityAttribute : ExtensionAttribute
{
    private readonly int cost;

    public FieldComplexityAttribute(int cost)
    {
        this.cost = cost;
    }

    public override void ApplyExtension(IField field) => field.SetComplexity(cost);
}
