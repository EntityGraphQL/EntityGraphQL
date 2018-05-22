using System.Collections.Generic;
using System.Linq.Expressions;
using EntityQueryLanguage.DataApi.Parsing;

namespace EntityQueryLanguage.DataApi
{
    public interface IRelationHandler
    {
        /// <summary>
        /// Called for each select. Check if there are any relations
        /// </summary>
        /// <returns></returns>
        LambdaExpression BuildNodeForSelect(List<Expression> relationFields, ParameterExpression contextParameter, LambdaExpression exp, string name, ISchemaProvider schemaProvider);

        /// <summary>
        /// Called once a whole Select statement is complete.
        /// </summary>
        LambdaExpression HandleSelectComplete(LambdaExpression baseExpression);
    }
}
