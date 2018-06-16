using System;
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
        public Expression Expression { get; private set; }
        public List<ParameterExpression> Parameters { get; private set; }
        public List<object> ConstantParameterValues { get; private set; }

        public List<GraphQLNode> Fields { get; private set; }
        public Expression RelationExpression { get; private set; }

        public GraphQLNode(string name, QueryResult query, Expression relationExpression) : this(name, query.Expression.Body, relationExpression, query.Expression.Parameters, query.ConstantParameterValues)
        {
        }

        public GraphQLNode(string name, Expression exp, Expression relationExpression, IEnumerable<ParameterExpression> constantParameters, IEnumerable<object> constantParameterValues)
        {
            Name = name;
            Expression = exp;
            Fields = new List<GraphQLNode>();
            RelationExpression = relationExpression;
            Parameters = constantParameters?.ToList();
            ConstantParameterValues = constantParameterValues?.ToList();
        }

        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (ConstantParameterValues != null)
                allArgs.AddRange(ConstantParameterValues);

            return Expression.Lambda(Expression, Parameters.ToArray()).Compile().DynamicInvoke(allArgs.ToArray());
        }

        public TReturnType Execute<TReturnType>(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (ConstantParameterValues != null)
                allArgs.AddRange(ConstantParameterValues);

            return (TReturnType)Expression.Lambda(Expression, Parameters.ToArray()).Compile().DynamicInvoke(allArgs.ToArray());
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={Expression}";
        }
    }
}
