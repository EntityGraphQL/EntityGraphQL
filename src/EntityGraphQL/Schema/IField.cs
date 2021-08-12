using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Represents a field in a GraphQL type. This can be a mutation field in the Mutation type or a field on a query type
    /// </summary>
    public interface IField
    {
        IDictionary<string, ArgType> Arguments { get; }
        string Name { get; }
        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
    }
}