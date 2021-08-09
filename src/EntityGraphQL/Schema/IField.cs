using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Represents a field in a Graph QL type. This can be a mutation field in the Mutation type
    /// </summary>
    public interface IField
    {
        IDictionary<string, ArgType> Arguments { get; }
        string Name { get; }
        string Description { get; }
        GqlTypeInfo ReturnType { get; }
        RequiredClaims AuthorizeClaims { get; }

        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
    }
}