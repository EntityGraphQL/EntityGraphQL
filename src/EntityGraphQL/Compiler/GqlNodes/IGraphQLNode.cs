using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public interface IGraphQLNode
    {
        string Name { get; }
        Expression? NextFieldContext { get; set; }
        IGraphQLNode? ParentNode { get; set; }
        ParameterExpression? RootParameter { get; set; }

        List<BaseGraphQLField> QueryFields { get; }
        void AddField(BaseGraphQLField field);
        IField? Field { get; }
        Dictionary<string, object> Arguments { get; }
    }
}