using System.Collections.Generic;
using System.Linq.Expressions;
using EntityQueryLanguage.GraphQL.Parsing;
using EntityQueryLanguage.Schema;

namespace EntityQueryLanguage.GraphQL
{
    public interface IRelationHandler
    {
        /// <summary>
        /// Called for each select. Check if there are any relations
        /// </summary>
        /// <returns></returns>
        Expression BuildNodeForSelect(List<Expression> relationFields, ParameterExpression contextParameter, Expression exp, string name, ISchemaProvider schemaProvider);

        /// <summary>
        /// Called once a whole Select statement is complete.
        /// </summary>
        Expression HandleSelectComplete(Expression baseExpression);
    }
}
