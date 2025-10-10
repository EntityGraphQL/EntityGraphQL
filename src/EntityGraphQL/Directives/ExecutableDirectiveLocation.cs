using System.ComponentModel;

namespace EntityGraphQL.Directives;

public enum ExecutableDirectiveLocation
{
    [Description("QUERY")]
    Query,

    [Description("MUTATION")]
    Mutation,

    [Description("SUBSCRIPTION")]
    Subscription,

    [Description("FIELD")]
    Field,

    [Description("FRAGMENT_DEFINITION")]
    FragmentDefinition,

    [Description("FRAGMENT_SPREAD")]
    FragmentSpread,

    [Description("INLINE_FRAGMENT")]
    InlineFragment,

    [Description("VARIABLE_DEFINITION")]
    VariableDefinition,
}
