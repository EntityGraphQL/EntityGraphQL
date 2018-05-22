using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityQueryLanguage.DataApi.Parsing
{
    public class DataApiNode
    {
        public string Name { get; private set; }
        public string Error { get; private set; }
        public Expression Expression { get; private set; }
        public List<DataApiNode> Fields { get; private set; }
        public IEnumerable<DataApiNode> Relations { get; private set; }
        public Expression RelationExpression { get; private set; }
        public ParameterExpression Parameter { get; private set; }


        public DataApiNode(string name, Expression query, ParameterExpression parameter, Expression relationExpression)
        {
            Name = name;
            Expression = query;
            Fields = new List<DataApiNode>();
            Parameter = parameter;
            RelationExpression = relationExpression;
        }

        public DataApiNode(string name, Expression query, ParameterExpression parameter, Expression relationExpression, IEnumerable<DataApiNode> relations) : this(name, query, parameter, relationExpression)
        {
            this.Relations = relations;
        }

        public LambdaExpression AsLambda()
        {
            return Expression.Lambda(Expression, Parameter);
        }

        public static DataApiNode MakeError(string name, string message)
        {
            return new DataApiNode(name, null, null, null) { Error = message };
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={Expression}";
        }
    }
}
