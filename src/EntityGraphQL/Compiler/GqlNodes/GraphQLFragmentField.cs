using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragmentField : BaseGraphQLField
    {
        public GraphQLFragmentField(string name)
        {
            Name = name;
        }

        public override bool HasAnyServices { get; set; } = false;
        public GraphQLFragmentStatement Fragment { get; private set; }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments)
        {
            var fragment = fragments.FirstOrDefault(f => f.Name == Name) ?? throw new EntityGraphQLCompilerException($"Fragment {Name} not found in query document");
            Fragment = fragment;

            return fragment.Fields;
        }

        public override ExpressionResult GetNodeExpression(object contextValue, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields = false, ParameterExpression buildServiceWrapWithParam = null)
        {
            throw new EntityGraphQLCompilerException($"Fragment should have expanded out into non fragment fields");
        }
    }
}