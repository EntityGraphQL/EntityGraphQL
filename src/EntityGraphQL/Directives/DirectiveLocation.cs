namespace EntityGraphQL.Directives;

public enum ExecutableDirectiveLocation
{
    QUERY,
    MUTATION,
    SUBSCRIPTION,
    FIELD,
#pragma warning disable CA1707
    FRAGMENT_DEFINITION,
    FRAGMENT_SPREAD,
    INLINE_FRAGMENT,
    VARIABLE_DEFINITION,
#pragma warning restore CA1707
}