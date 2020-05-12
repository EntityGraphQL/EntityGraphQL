using System;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Directives
{
    public class IncludeDirectiveProcessor : DirectiveProcessor<IncludeArguments>
    {
        public override bool ProcessesResult { get => false; }
        public override Type GetArgumentsType()
        {
            return typeof(IncludeArguments);
        }

        public override IGraphQLBaseNode ProcessQuery(GraphQLQueryNode field, IncludeArguments arguments)
        {
            if (arguments.@if)
                return field;
            return null;
        }
    }

    public class IncludeArguments
    {
        public bool @if { get; set; }
    }
}