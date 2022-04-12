using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLQueryStatement : ExecutableGraphQLStatement
    {
        public GraphQLQueryStatement(ISchemaProvider schema, string name, Expression nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode, Dictionary<string, ArgType> variables)
            : base(schema, name, nodeExpression, rootParameter, parentNode, variables)
        {
        }
    }
}
