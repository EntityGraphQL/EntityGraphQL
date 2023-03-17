using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public interface IGraphQLNode
    {
        ISchemaProvider Schema { get; }
        /// <summary>
        /// Name of the field
        /// </summary>
        string Name { get; }
        /// <summary>
        /// The expression that represents the field. This will be the context for the next field selection
        /// </summary>
        Expression? NextFieldContext { get; }
        /// <summary>
        /// Parent field. e.g. if we have a field manger like in `people { manager }` then the parent is people
        /// </summary>
        IGraphQLNode? ParentNode { get; }
        ParameterExpression? RootParameter { get; }

        List<BaseGraphQLField> QueryFields { get; }
        void AddField(BaseGraphQLField field);
        IField? Field { get; }
        bool HasServices { get; }
        IReadOnlyDictionary<string, object> Arguments { get; }
        void AddDirectives(IEnumerable<GraphQLDirective> graphQLDirectives);
    }
}