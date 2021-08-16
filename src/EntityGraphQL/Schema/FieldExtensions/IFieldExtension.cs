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
        Expression GetExpression(Field field, ExpressionResult expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer);
    }
}