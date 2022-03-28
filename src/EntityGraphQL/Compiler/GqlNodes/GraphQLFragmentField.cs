using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragmentField : BaseGraphQLField
    {
        public GraphQLFragmentField(string name, Field? field, Expression? nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(name, field, nodeExpression, rootParameter, parentNode)
        {
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return fragments.FirstOrDefault(f => f.Name == Name).QueryFields.Any(f => f.HasAnyServices(fragments));
        }
        public GraphQLFragmentStatement? Fragment { get; private set; }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields)
        {
            if (Fragment == null)
            {
                var fragment = fragments.FirstOrDefault(f => f.Name == Name) ?? throw new EntityGraphQLCompilerException($"Fragment {Name} not found in query document");
                Fragment = fragment;
            }

            return Fragment.QueryFields.SelectMany(f => f.Expand(fragments, withoutServiceFields));
        }

        public override Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            throw new EntityGraphQLCompilerException($"Fragment should have expanded out into non fragment fields");
        }
    }
}