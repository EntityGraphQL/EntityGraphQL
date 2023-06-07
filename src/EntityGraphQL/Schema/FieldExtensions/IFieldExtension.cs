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
        void Configure(ISchemaProvider schema, IField field);

        /// <summary>
        /// Called when the field is used in a query. This is at the compiling of the query stage, it is before the
        /// field expression is joined with a Select() or built into a new {}.
        /// Use this as a chance to make any expression changes based on arguments or do rules/error checks on arguments.
        /// 
        /// This should be thread safe
        /// </summary>
        /// <param name="field"></param>
        /// <param name="expression">The current expression for the field</param>
        /// <param name="argumentParam">The ParameterExpression used for accessing the arguments. Null if the field has no augments</param>
        /// <param name="arguments">The value of the arguments. Null if field have no arguments</param>
        /// <param name="context">The context of the schema</param>
        /// <param name="servicesPass">True if this is the second visit. This means the object graph is built and we are now bringing in fields that use services</param>
        /// <returns></returns>
        Expression? GetExpression(IField field, Expression expression, ParameterExpression? argumentParam, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer);
        /// <summary>
        /// Called when the field is being finalized for execution but we have not yet selected the fields for selection on this expression
        /// Not called for GraphQLFieldType.Scalar
        /// 
        /// This should be thread safe
        /// </summary>
        /// <param name="baseExpression">ListSelection: The expression used to add .Select() to. ObjectProjection: the base expression which fields are selected from</param>
        /// <param name="listTypeParam">For ListSelection the expression for the context of the list selection about to happen</param>
        /// <returns></returns>
        (Expression, ParameterExpression?) ProcessExpressionPreSelection(Expression baseExpression, ParameterExpression? listTypeParam, ParameterReplacer parameterReplacer);
        /// <summary>
        /// Called when the field is being finalized for execution but we have not yet created a new {} expression for the select.
        /// Not called for GraphQLFieldType.Scalar
        /// 
        /// This should be thread safe
        /// </summary>
        /// <param name="baseExpression">ListSelection: The expression used to add .Select() to. ObjectProjection: the base expression which fields are selected from</param>
        /// <param name="selectionExpressions">ListSelection: The selection fields used in .Select(). ObjectProjection: The fields used in the new { field1 = ..., field2 = ... }</param>
        /// <param name="selectContextParam"></param>
        /// <param name="argumentParam"></param>
        /// <param name="parameterReplacer"></param>
        /// <returns></returns>
        (Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam) ProcessExpressionSelection(Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam, ParameterExpression? argumentParam, bool servicesPass, ParameterReplacer parameterReplacer);
        /// <summary>
        /// Called when the field is being finalized for execution
        /// 
        /// This should be thread safe
        /// </summary>
        /// <param name="fieldType"></param>
        /// <param name="expression">The final expression for the field</param>
        /// <returns></returns>
        Expression ProcessScalarExpression(Expression expression, ParameterReplacer parameterReplacer);
    }
}