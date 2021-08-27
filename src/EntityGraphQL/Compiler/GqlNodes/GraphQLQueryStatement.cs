using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLQueryStatement : ExecutableGraphQLStatement
    {
        public GraphQLQueryStatement(string name, Expression nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(name, nodeExpression, rootParameter, parentNode)
        {
        }
    }
}
