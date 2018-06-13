using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityQueryLanguage.GraphQL.Parsing
{
    /// <summary>
    /// Represents a top level node in the GraphQL query.
    /// {
    ///     people { id, name },
    ///     houses { location }
    /// }
    /// Each of people & houses are seperate queries that can/will be executed
    /// </summary>
    public class GraphQLNode
    {
        public string Name { get; private set; }
        public string Error { get; private set; }
        public Expression Expression { get; private set; }
        public List<ParameterExpression> Parameters { get; private set; }
        public List<object> ConstParameterValues { get; private set; }

        public List<GraphQLNode> Fields { get; private set; }
        public Expression RelationExpression { get; private set; }

        public GraphQLNode(string name, QueryResult query, Expression relationExpression) : this(name, query.Expression.Body, relationExpression)
        {
            Parameters = query.Expression.Parameters.ToList();
            ConstParameterValues = query.ParameterValues?.ToList();
        }

        public GraphQLNode(string name, Expression exp, Expression relationExpression)
        {
            Name = name;
            Expression = exp;
            Fields = new List<GraphQLNode>();
            RelationExpression = relationExpression;
        }


        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (ConstParameterValues != null)
                allArgs.AddRange(ConstParameterValues);

            return Expression.Lambda(Expression, Parameters.ToArray()).Compile().DynamicInvoke(allArgs.ToArray());
        }

        public static GraphQLNode MakeError(string name, string message)
        {
            return new GraphQLNode(name, (Expression)null, null) { Error = message };
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={Expression}";
        }
    }
}
