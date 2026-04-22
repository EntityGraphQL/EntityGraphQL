using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Context passed to <see cref="IFieldExtension.ProcessExpressionSelection"/>.
/// Represents the field expression while child selections are being assembled.
/// </summary>
public sealed class FieldExtensionSelectionContext
{
    /// <summary>
    /// The current base expression being selected from.
    /// </summary>
    public Expression BaseExpression { get; set; } = null!;

    /// <summary>
    /// The compiled child selection expressions keyed by response field.
    /// </summary>
    public Dictionary<IFieldKey, CompiledField> SelectionExpressions { get; set; } = null!;

    /// <summary>
    /// The current selection context parameter.
    /// </summary>
    public ParameterExpression? SelectContextParameter { get; set; }

    /// <summary>
    /// The argument parameter for the field, when one exists.
    /// </summary>
    public ParameterExpression? ArgumentParameter { get; set; }

    /// <summary>
    /// True when compiling the service-enabled execution pass.
    /// </summary>
    public bool ServicesPass { get; set; }

    /// <summary>
    /// Helper used to rewrite the expression/context.
    /// </summary>
    public ParameterReplacer ParameterReplacer { get; set; } = null!;
}
