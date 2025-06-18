namespace EntityGraphQL;

public interface IGraphQLArgumentsSet
{
    /// <summary>
    /// Checks if the argument with the given name has been set in the query either inline or as a variable.
    /// If false, then the argument is not set by the query and the value will be the default value of that type in dotnet.
    /// </summary>
    bool IsSet(string name);
}
