using System;
using System.Collections.Generic;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Directives
{
    public class SkipDirectiveProcessor : DirectiveProcessor<SkipArguments>
    {
        public override string Name { get => "skip"; }
        public override string Description { get => "Directs the executor to skip this field or fragment when the `if` argument is true."; }
        public override List<ExecutableDirectiveLocation> Location => new() { ExecutableDirectiveLocation.FIELD, ExecutableDirectiveLocation.FRAGMENT_SPREAD, ExecutableDirectiveLocation.INLINE_FRAGMENT };

        public override IGraphQLNode? VisitNode(ExecutableDirectiveLocation location, IGraphQLNode? node, object? arguments)
        {
            if (arguments is null)
                throw new ArgumentNullException("if", "Argument 'if' is requred for @skip directive");
            return !((SkipArguments)arguments).If ? node : null;
        }
    }

    public class SkipArguments
    {
        [GraphQLField("if", "Excluded when true.")]
        public bool If { get; set; }
    }
}