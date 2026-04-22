using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Context passed to <see cref="IFieldExtension.ProcessExpressionPreSelection"/>.
/// Represents the field expression just before child selections are applied.
/// </summary>
public sealed class FieldExtensionPreSelectionContext
{
    /// <summary>
    /// The expression used as the base for the upcoming selection.
    /// </summary>
    public Expression BaseExpression { get; set; } = null!;

    /// <summary>
    /// The current list/object selection context parameter when applicable.
    /// </summary>
    public ParameterExpression? ListTypeParameter { get; set; }

    /// <summary>
    /// Helper used to rewrite the expression/context.
    /// </summary>
    public ParameterReplacer ParameterReplacer { get; set; } = null!;
}
