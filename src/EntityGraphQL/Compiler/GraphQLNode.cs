using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Compiler
{
    public interface IGraphQLNode
    {
        bool IsMutation { get; }
        ExpressionResult NodeExpression { get; set; }
        Expression RelationExpression { get; }
        string Name { get; }
        List<object> ConstantParameterValues { get; }
        List<ParameterExpression> Parameters { get; }
        List<IGraphQLNode> Fields { get; }

        object Execute(params object[] args);
    }

    /// <summary>
    /// Represents a top level node in the GraphQL query.
    /// {
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

        public bool IsMutation => false;

        public GraphQLNode(string name, QueryResult query, Expression relationExpression) : this(name, (ExpressionResult)query.ExpressionResult, relationExpression, query.LambdaExpression.Parameters, query.ConstantParameterValues)
        {
        }

        public GraphQLNode(string name, ExpressionResult exp, Expression relationExpression, IEnumerable<ParameterExpression> constantParameters, IEnumerable<object> constantParameterValues)
        {
            Name = name;
            NodeExpression = exp;
            Fields = new List<IGraphQLNode>();
            if (relationExpression != null)
            {
                RelationExpression = relationExpression;
            }
            Parameters = constantParameters?.ToList();
            ConstantParameterValues = constantParameterValues?.ToList();
        }

        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (ConstantParameterValues != null)
                allArgs.AddRange(ConstantParameterValues);

            return Expression.Lambda(NodeExpression, Parameters.ToArray()).Compile().DynamicInvoke(allArgs.ToArray());
        }

        public TReturnType Execute<TReturnType>(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (ConstantParameterValues != null)
                allArgs.AddRange(ConstantParameterValues);

            return (TReturnType)Expression.Lambda(NodeExpression, Parameters.ToArray()).Compile().DynamicInvoke(allArgs.ToArray());
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={NodeExpression}";
        }
    }

    public class GraphQLMutationNode : IGraphQLNode
    {
        private QueryResult result;
        private IGraphQLNode graphQLNode;

        public bool IsMutation => true;
        public List<IGraphQLNode> Fields { get; private set; }

        public Expression RelationExpression => throw new NotImplementedException();

        public string Name => graphQLNode.Name;

        public List<object> ConstantParameterValues => throw new NotImplementedException();

        public List<ParameterExpression> Parameters => throw new NotImplementedException();

        ExpressionResult IGraphQLNode.NodeExpression { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public GraphQLMutationNode(QueryResult result, IGraphQLNode graphQLNode)
        {
            this.result = result;
            this.graphQLNode = graphQLNode;
            Fields = new List<IGraphQLNode>();
        }

        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (graphQLNode.ConstantParameterValues != null)
                allArgs.AddRange(graphQLNode.ConstantParameterValues);

            // run the mutation to get the context for the query select
            var mutation = (MutationResult)this.result.ExpressionResult;
            var result = mutation.Execute(args);
            // run the query select
            result = graphQLNode.Execute(result);
            return result;
        }
    }
}
