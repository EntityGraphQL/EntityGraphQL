using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public interface IFieldExtension
    {
        /// <summary>
        /// Configure the field. Called once on adding the extension to the field. You can set up the
        /// expression here or prepare what you need to set up the expression in the next steps
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="field"></param>
        void Configure(ISchemaProvider schema, Field field);

        /// <summary>
        /// Called when the field is used in a query. This is at the compiling of the query stage, it is before the
        /// field expression is joined with a Select() or built into a new {}.
        /// Use this as a chance to make any expression changes based on arguments or do rules/error checks on arguments.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="expression">The current expression for the field</param>
        /// <param name="argExpression">The ParameterExpression used for accessing the arguments. Null if the field has no augments</param>
        /// <param name="arguments">The values of the arguments. Null if field have no arguments</param>
        /// <returns></returns>
        Expression GetExpression(Field field, Expression expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer);
        /// <summary>
        /// Called when the field is being finalized for execution but we have not yet selected the fields for selection on this expression
        /// Not called for GraphQLFieldType.Scalar
        /// </summary>
        /// <param name="fieldType">Type of field being built. ListSelection or ObjectProjection</param>
        /// <param name="baseExpression">ListSelection: The expression used to add .Select() to. ObjectProjection: the base expression which fields are selected from</param>
        /// <returns></returns>
        Expression ProcessExpressionPreSelection(GraphQLFieldType fieldType, Expression baseExpression, ParameterReplacer parameterReplacer);
        /// <summary>
        /// Called when the field is being finalized for execution but we have not yet created a new {} expression for the select.
        /// Not called for GraphQLFieldType.Scalar
        /// </summary>
        /// <param name="fieldType">Type of field being built. ListSelection or ObjectProjection</param>
        /// <param name="baseExpression">ListSelection: The expression used to add .Select() to. ObjectProjection: the base expression which fields are selected from</param>
        /// <param name="selectionExpressions">ListSelection: The selection fields used in .Select(). ObjectProjection: The fields used in the new { field1 = ..., field2 = ... }</param>
        /// <returns></returns>
        (Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionSelection(GraphQLFieldType fieldType, Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, ParameterReplacer parameterReplacer);
        /// <summary>
        /// Called when the field is being finalized for execution
        /// </summary>
        /// <param name="fieldType"></param>
        /// <param name="expression">The final expression for the field</param>
        /// <returns></returns>
        Expression ProcessScalarExpression(Expression expression, ParameterReplacer parameterReplacer);
    }
}