using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

public abstract class BaseFieldExtension : IFieldExtension
{
    /// <summary>
    /// Configure the field. Called once on adding the extension to the field. You can set up the
    /// expression here or prepare what you need to set up the expression in the next steps
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="field"></param>
    public virtual void Configure(ISchemaProvider schema, IField field) { }

    public virtual (Expression, ParameterExpression?) ProcessExpressionPreSelection(FieldExtensionPreSelectionContext context)
    {
        return (context.BaseExpression, context.ListTypeParameter);
    }

    public virtual Expression GetListExpressionForBulkResolve(Expression listExpression)
    {
        return listExpression;
    }

    /// <summary>
    /// Called when the field is being finalized for execution but we have not yet created a new {} expression for the select.
    /// Not called for GraphQLFieldType.Scalar
    /// </summary>
    /// <param name="context">Request-scoped selection context.</param>
    /// <returns></returns>
    public virtual (Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam) ProcessExpressionSelection(
        FieldExtensionSelectionContext context
    )
    {
        return (context.BaseExpression, context.SelectionExpressions, context.SelectContextParameter);
    }

    /// <summary>
    /// Called when a scalar field expression is being finalized for execution
    /// </summary>
    public virtual Expression ProcessScalarExpression(Expression expression, ParameterReplacer parameterReplacer)
    {
        return expression;
    }

    public virtual (Expression? expression, ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue) GetExpressionAndArguments(
        IField field,
        FieldExtensionExpressionContext context
    )
    {
        return (context.Expression, context.OriginalArgumentParameter, context.ArgumentParameter, context.Arguments);
    }

    /// <summary>
    /// True when this service-backed collection was already built (and so already had this extension applied)
    /// in the first pass, and what we hold now is that pass's projected result. A root service list takes part
    /// in the two-pass flow, so its elements here are the first-pass dynamic type rather than the type the
    /// extension was configured against - re-applying the work would fail on the changed shape. A nested
    /// service-backed collection has no first pass of its own, so its elements are still the configured type
    /// and this pass is its only chance to be filtered/sorted.
    /// </summary>
    protected static bool CollectionBuiltInFirstPass(IField field, Expression expression, Type? configuredElementType)
    {
        if (field.Services.Count == 0)
            return false;
        var currentElementType = expression.Type.GetEnumerableOrArrayType();
        return currentElementType != null && currentElementType != configuredElementType && configuredElementType?.IsAssignableFrom(currentElementType) == false;
    }
}
