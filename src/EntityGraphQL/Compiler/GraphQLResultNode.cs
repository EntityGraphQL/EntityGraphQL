using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLResultNode : IGraphQLNode
    {
        private List<GraphQLFragment> fragments;
        public IGraphQLNode Action { get; }

        public GraphQLResultNode(IGraphQLNode action, List<GraphQLFragment> fragments)
        {
            this.Action = action;
            this.fragments = fragments;
        }

        public string Name => "Query Request Root";

        public List<IGraphQLNode> Fields => Action.Fields;

        public List<object> ConstantParameterValues => throw new NotImplementedException();

        public List<ParameterExpression> Parameters => throw new NotImplementedException();

        public ExpressionResult NodeExpression { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <summary>
        /// Expects 2 arguments and just calls ExecuteQuery().
        /// </summary>
        /// <param name="args">Must be 2. The context and a QueryResult to add results to</param>
        /// <returns></returns>
        public object Execute(params object[] args)
        {
            if (args.Length != 2)
                throw new ArgumentException("Must supply 2 arguments. The context and a QueryResult instance");
            var context = args[0];
            var result = (QueryResult)args[1];
            return ExecuteQuery(context, result);
         }

        /// <summary>
        /// Executes the compiled GraphQL document adding data results into QueryResult
        /// </summary>
        /// <param name="context">The context object to apply the compiled Lambda to. E.g. a DbContext</param>
        /// <param name="result">A QueryResult instance in which data results or errors will be added to</param>
        /// <returns></returns>
        public object ExecuteQuery(object context, QueryResult result)
        {
            foreach (var node in Action.Fields)
            {
                result.Data[node.Name] = null;
                // request.Variables are already compiled into the expression
                var data = node.Execute(context);
                result.Data[node.Name] = data;
            }
            return result;
        }
    }
}