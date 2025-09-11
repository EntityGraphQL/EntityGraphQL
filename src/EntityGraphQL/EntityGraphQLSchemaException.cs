using System;

namespace EntityGraphQL;

/// <summary>
/// Represents errors that occur during the building of a GraphQL schema.
/// </summary>
public class EntityGraphQLSchemaException : Exception
{
    public EntityGraphQLSchemaException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
