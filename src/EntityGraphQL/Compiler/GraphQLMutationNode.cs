using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLMutationNode : IGraphQLNode
    {
        private CompiledQueryResult result;
        private IGraphQLNode graphQLNode;

        public List<IGraphQLNode> Fields { get; private set; }

        public string Name => graphQLNode.Name;

        public List<object> ConstantParameterValues => throw new NotImplementedException();

        public List<ParameterExpression> Parameters => throw new NotImplementedException();

        public ExpressionResult NodeExpression { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public GraphQLMutationNode(CompiledQueryResult result, IGraphQLNode graphQLNode)
        {
            this.result = result;
            this.graphQLNode = graphQLNode;
            Fields = new List<IGraphQLNode>();
        }

        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (graphQLNode.ConstantParameterValues != null)
            {
                allArgs.AddRange(graphQLNode.ConstantParameterValues);
            }

            // run the mutation to get the context for the query select
            var mutation = (MutationResult)this.result.ExpressionResult;
            var result = mutation.Execute(args);
            // run the query select
            result = graphQLNode.Execute(result);
            return result;
        }
    }
}
