using System;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Directives
{
    public class SkipDirectiveProcessor : DirectiveProcessor<SkipArguments>
    {
        public override bool ProcessesResult { get => false; }
        public override Type GetArgumentsType()
        {
            return typeof(SkipArguments);
        }

        public override IGraphQLBaseNode ProcessQuery(GraphQLQueryNode field, SkipArguments arguments)
        {
            if (arguments.@if)
                return null;
            return field;
        }
    }

    public class SkipArguments
    {
        public bool @if { get; set; }
    }
}