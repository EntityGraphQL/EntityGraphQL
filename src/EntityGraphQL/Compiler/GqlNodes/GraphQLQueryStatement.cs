using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLQueryStatement : ExecutableGraphQLStatement
    {
        public GraphQLQueryStatement(string name, Expression nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode, Dictionary<string, (Type, object?)> variables)
            : base(name, nodeExpression, rootParameter, parentNode, variables)
        {
        }
    }
}
