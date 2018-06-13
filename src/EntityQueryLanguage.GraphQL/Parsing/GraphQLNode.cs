using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityQueryLanguage.GraphQL.Parsing
{
    public class GraphQLNode
    {
        public string Name { get; private set; }
        public string Error { get; private set; }
        public Expression Expression { get; private set; }
        public List<GraphQLNode> Fields { get; private set; }
        public IEnumerable<GraphQLNode> Relations { get; private set; }
        public Expression RelationExpression { get; private set; }
        public ParameterExpression Parameter { get; private set; }


        public GraphQLNode(string name, Expression query, ParameterExpression parameter, Expression relationExpression)
        {
            Name = name;
            Expression = query;
            Fields = new List<GraphQLNode>();
            Parameter = parameter;
            RelationExpression = relationExpression;
        }

        public GraphQLNode(string name, Expression query, ParameterExpression parameter, Expression relationExpression, IEnumerable<GraphQLNode> relations) : this(name, query, parameter, relationExpression)
        {
            this.Relations = relations;
        }

        public LambdaExpression AsLambda()
        {
            return Expression.Lambda(Expression, Parameter);
        }

        public static GraphQLNode MakeError(string name, string message)
        {
            return new GraphQLNode(name, null, null, null) { Error = message };
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={Expression}";
        }
    }
}
