using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions;

public interface IFieldExtension
{
    /// <summary>
    /// Configure the field. Called once on adding the extension to the field. You can set up the
    /// expression here or prepare what you need to set up the expression in the next steps
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="field"></param>
    void Configure(ISchemaProvider schema, IField field);

    /// <summary>
    /// Get the list expression for the bulk resolve. Useful if your extension rebuilt the the graph like ConnectionPaging
    /// </summary>
    /// <param name="listExpression"></param>
    /// <returns></returns>
    Expression GetListExpressionForBulkResolve(Expression listExpression);

    /// <summary>
    /// Called when the field is used in a query. This is at the compiling of the query stage, it is before the
    /// field expression is joined with a Select() or built into a new {}.
    /// Use this as a chance to make any expression changes based on arguments or do rules/error checks on arguments.
    /// Allowing the extension to modify arguments for example to bring arguments from a parent node down to the current field
    ///
    /// This should be thread safe
    /// </summary>
    /// <param name="field"></param>
    /// <param name="context">Request-scoped field compilation context. Contains the current expression,
    /// argument parameter/value, field context, pass flags, and the active <see cref="CompileContext"/>.</param>
    /// <returns></returns>
    (Expression? expression, ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue) GetExpressionAndArguments(
        IField field,
        FieldExtensionExpressionContext context
    );

    /// <summary>
    /// Called when the field is being finalized for execution but we have not yet selected the fields for selection on this expression
    /// Not called for GraphQLFieldType.Scalar
    ///
    /// This should be thread safe
    /// </summary>
    /// <param name="context">Request-scoped pre-selection context. Contains the current base expression,
    /// the selection context parameter, and the active <see cref="ParameterReplacer"/>.</param>
    /// <returns></returns>
    (Expression, ParameterExpression?) ProcessExpressionPreSelection(FieldExtensionPreSelectionContext context);

    /// <summary>
    /// Called when the field is being finalized for execution but we have not yet created a new {} expression for the select.
    /// Not called for GraphQLFieldType.Scalar
    ///
    /// This should be thread safe
    /// </summary>
    /// <param name="context">Request-scoped selection context. Contains the current base expression,
    /// compiled child selections, selection/argument parameters, pass flags, and the active <see cref="ParameterReplacer"/>.</param>
    /// <returns></returns>
    (Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam) ProcessExpressionSelection(FieldExtensionSelectionContext context);

    /// <summary>
    /// Called when the field is being finalized for execution
    ///
    /// This should be thread safe
    /// </summary>
    /// <param name="expression">The final expression for the field</param>
    /// <returns></returns>
    Expression ProcessScalarExpression(Expression expression, ParameterReplacer parameterReplacer);
}
