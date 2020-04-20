using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragment
    {
        private string TypeName { get; }

        public string Name { get; }

        public IEnumerable<IGraphQLBaseNode> Fields { get; }
        /// <summary>
        /// The ParameterExpression used for the context.n This needs to be replaced by the real parameter on execution
        /// </summary>
        /// <value></value>
        public ParameterExpression SelectContext { get; }

        public GraphQLFragment(string name, string typeName, IEnumerable<IGraphQLBaseNode> fields, ParameterExpression selectContext)
        {
            Name = name;
            TypeName = typeName;
            Fields = fields;
            SelectContext = selectContext;
        }
    }

    public class GraphQLFragmentSelect : IGraphQLBaseNode
    {
        private readonly string name;

        public GraphQLFragmentSelect(string name)
        {
            this.name = name;
        }

        public string Name { get { return this.name; } set => throw new System.NotImplementedException(); }

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters => new Dictionary<ParameterExpression, object>();

        public ExpressionResult GetNodeExpression()
        {
            throw new System.NotImplementedException();
        }

        public void SetNodeExpression(ExpressionResult expressionResult)
        {
            throw new System.NotImplementedException();
        }
    }
}