using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Request-scoped context passed to <see cref="IFieldExtension.GetExpressionAndArguments"/>.
/// Contains the field expression being built plus the current execution/argument state for this field.
/// </summary>
public sealed class FieldExtensionExpressionContext
{
    /// <summary>
    /// The GraphQL field node currently being compiled.
    /// </summary>
    public BaseGraphQLField FieldNode { get; set; } = null!;

    /// <summary>
    /// The current expression for the field. Extensions can transform this expression.
    /// </summary>
    public Expression Expression { get; set; } = null!;

    /// <summary>
    /// The execution-time argument parameter for the field. Null when the field has no arguments.
    /// </summary>
    public ParameterExpression? ArgumentParameter { get; set; }

    /// <summary>
    /// The current argument value object for the field. Null when the field has no arguments.
    /// </summary>
    public dynamic? Arguments { get; set; }

    /// <summary>
    /// The current field context expression.
    /// </summary>
    public Expression Context { get; set; } = null!;

    /// <summary>
    /// True when compiling the service-enabled execution pass.
    /// </summary>
    public bool ServicesPass { get; set; }

    /// <summary>
    /// True when compiling the first pass that excludes service-backed fields.
    /// </summary>
    public bool WithoutServiceFields { get; set; }

    /// <summary>
    /// Helper used to replace parameters/contexts while rebuilding expressions.
    /// </summary>
    public ParameterReplacer ParameterReplacer { get; set; } = null!;

    /// <summary>
    /// The field's original schema-time argument parameter before request-scoped cloning.
    /// </summary>
    public ParameterExpression? OriginalArgumentParameter { get; set; }

    /// <summary>
    /// Request-scoped compilation state for the current execution.
    /// </summary>
    public CompileContext CompileContext { get; set; } = null!;
}
