using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Context passed to <see cref="IFieldExtension.ProcessExpressionPostSelection"/>.
/// Represents a list field after its selection has been built.
/// </summary>
public sealed class FieldExtensionPostSelectionContext
{
    /// <summary>
    /// The projected result expression - the Select() of the fields asked for, and the ToList() for a nested list.
    /// </summary>
    public Expression ResultExpression { get; set; } = null!;

    /// <summary>
    /// The compiled child selection expressions the result was built from, keyed by response field.
    /// </summary>
    public Dictionary<IFieldKey, CompiledField> SelectionExpressions { get; set; } = null!;

    /// <summary>
    /// True when compiling the service-enabled execution pass.
    /// </summary>
    public bool ServicesPass { get; set; }
}
