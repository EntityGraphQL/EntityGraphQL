using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragmentStatement : IGraphQLNode
    {
        public Expression NextFieldContext { get; set; }
        public IGraphQLNode ParentNode { get; set; }
        public ParameterExpression RootParameter { get; set; }
        public List<BaseGraphQLField> QueryFields { get; protected set; } = new List<BaseGraphQLField>();

        public string Name { get; }

        public GraphQLFragmentStatement(string name, ParameterExpression selectContext, ParameterExpression rootParameter)
        {
            Name = name;
            NextFieldContext = selectContext;
            RootParameter = rootParameter;
        }

        internal List<Expression> Compile<TContext>(TContext context, GraphQLValidator validator, IServiceProvider services, List<GraphQLFragmentStatement> fragments, out List<object> allArgs)
        {
            throw new NotImplementedException();
        }

        public void AddField(BaseGraphQLField field)
        {
            QueryFields.Add(field);
        }
    }
}