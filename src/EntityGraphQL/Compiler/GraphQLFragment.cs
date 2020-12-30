using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragment
    {
        public string Name { get; }

        public IEnumerable<IGraphQLBaseNode> Fields { get; }
        /// <summary>
        /// The ParameterExpression used for the context.n This needs to be replaced by the real parameter on execution
        /// </summary>
        /// <value></value>
        public ParameterExpression SelectContext { get; }

        public GraphQLFragment(string name, IEnumerable<IGraphQLBaseNode> fields, ParameterExpression selectContext)
        {
            Name = name;
            Fields = fields;
            SelectContext = selectContext;
        }
    }

    public class GraphQLFragmentSelect : IGraphQLBaseNode
    {
        private readonly string name;

        public ParameterExpression FieldParameter => throw new NotImplementedException();

        public GraphQLFragmentSelect(string name)
        {
            this.name = name;
        }

        public string Name { get { return name; } set => throw new NotImplementedException(); }
        public bool HasWrappedService { get; } = false;

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters => new Dictionary<ParameterExpression, object>();

        public IEnumerable<Type> Services => throw new NotImplementedException();

        public ExpressionResult GetNodeExpression(object contextValue, IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }

        public void SetCombineExpression(Expression item2)
        {
            throw new NotImplementedException();
        }

        public void SetNodeExpression(ExpressionResult expressionResult)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IGraphQLBaseNode> GetSubExpressionForParameter(ParameterExpression contextParam)
        {
            return new List<IGraphQLBaseNode>();
        }
    }
}