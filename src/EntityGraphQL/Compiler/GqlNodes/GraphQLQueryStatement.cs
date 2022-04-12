using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLQueryStatement : ExecutableGraphQLStatement
    {
        public GraphQLQueryStatement(string name, Expression nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode, Dictionary<string, ArgType> variables)
            : base(name, nodeExpression, rootParameter, parentNode, variables)
        {
        }
    }
}
