using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragmentField : BaseGraphQLField
    {
        public GraphQLFragmentField(ISchemaProvider schema, string name, Expression? nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(schema, null, name, nodeExpression, rootParameter, parentNode, null)
        {
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return fragments.FirstOrDefault(f => f.Name == Name).QueryFields.Any(f => f.HasAnyServices(fragments));
        }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, ParameterExpression? docParam, object? docVariables)
        {
            var result = (GraphQLFragmentField)ProcessFieldDirectives(this, docParam, docVariables);
            if (result == null)
                return new List<BaseGraphQLField>();

            var fragment = fragments.FirstOrDefault(f => f.Name == Name) ?? throw new EntityGraphQLCompilerException($"Fragment {Name} not found in query document");
            return fragment.QueryFields.SelectMany(f => f.Expand(fragments, withoutServiceFields, docParam, docVariables));
        }

        public override Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            throw new EntityGraphQLCompilerException($"Fragment should have expanded out into non fragment fields");
        }
    }
}