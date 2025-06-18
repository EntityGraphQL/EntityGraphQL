using System.Collections.Generic;

namespace EntityGraphQL;

public class GraphQLArgumentsSet : IGraphQLArgumentsSet
{
    private readonly Dictionary<string, object?> setArguments = [];

    public bool IsSet(string name)
    {
        if (setArguments.ContainsKey(name))
        {
            return true;
        }

        return false;
    }

    internal void AddSetArgument(string name, object? value)
    {
        setArguments.Add(name, value);
    }
}
