using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents a top level node in the GraphQL query.
    /// query MyQuery {
    ///     people { id, name },
    ///     houses { location }
    /// }
    /// Each of people & houses are seperate queries that can/will be executed
    /// </summary>
    public class GraphQLNode : IGraphQLNode
    {
        public string Name { get; private set; }
        public ExpressionResult NodeExpression { get; set; }
        public List<ParameterExpression> Parameters { get; private set; }
        public List<object> ConstantParameterValues { get; private set; }

        public List<IGraphQLNode> Fields { get; private set; }
        public Expression RelationExpression { get; private set; }

        public GraphQLNode(string name, CompiledQueryResult query, Expression relationExpression) : this(name, (ExpressionResult)query.ExpressionResult, relationExpression, query.LambdaExpression.Parameters, query.ConstantParameterValues)
        {
        }

        public GraphQLNode(string name, ExpressionResult exp, Expression relationExpression, IEnumerable<ParameterExpression> expressionParameters, IEnumerable<object> constantParameterValues)
        {
            Name = name;
            NodeExpression = exp;
            Fields = new List<IGraphQLNode>();
            if (relationExpression != null)
            {
                RelationExpression = relationExpression;
            }
            Parameters = expressionParameters?.ToList();
            ConstantParameterValues = constantParameterValues?.ToList();
        }

        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (ConstantParameterValues != null && ConstantParameterValues.Any())
            {
                allArgs.AddRange(ConstantParameterValues);
            }

            return Expression.Lambda(NodeExpression, Parameters.ToArray()).Compile().DynamicInvoke(allArgs.ToArray());
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={NodeExpression}";
        }
    }
}
