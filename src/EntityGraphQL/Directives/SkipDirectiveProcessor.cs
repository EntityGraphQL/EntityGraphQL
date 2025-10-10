using System.Collections.Generic;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Directives;

public class SkipDirectiveProcessor : DirectiveProcessor<SkipArguments>
{
    public override string Name => "skip";
    public override string Description => "Directs the executor to skip this field or fragment when the `if` argument is true.";
    public override List<ExecutableDirectiveLocation> Location => [ExecutableDirectiveLocation.Field, ExecutableDirectiveLocation.FragmentSpread, ExecutableDirectiveLocation.InlineFragment];

    public override IGraphQLNode? VisitNode(ExecutableDirectiveLocation location, IGraphQLNode? node, object? arguments)
    {
        if (arguments is null)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, "Argument 'if' is required for @skip directive");
        return !((SkipArguments)arguments).If ? node : null;
    }
}

public class SkipArguments
{
    [GraphQLField("if", "Excluded when true.")]
    public bool If { get; set; }
}
