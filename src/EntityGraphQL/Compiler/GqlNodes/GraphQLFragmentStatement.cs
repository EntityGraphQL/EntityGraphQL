using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragmentStatement : IGraphQLNode
    {
        public Expression? NextFieldContext { get; set; }
        public IGraphQLNode? ParentNode { get; set; }
        public ParameterExpression? RootParameter { get; set; }

        public IField? Field { get; }

        public Dictionary<string, object> Arguments { get; }

        public string Name { get; }

        public List<BaseGraphQLField> QueryFields { get; } = new List<BaseGraphQLField>();

        public GraphQLFragmentStatement(string name, ParameterExpression selectContext, ParameterExpression rootParameter)
        {
            Name = name;
            NextFieldContext = selectContext;
            RootParameter = rootParameter;
            Arguments = new Dictionary<string, object>();
        }

        public void AddField(BaseGraphQLField field)
        {
            QueryFields.Add(field);
        }
    }
}