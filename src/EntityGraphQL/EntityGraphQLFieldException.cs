using System;

namespace EntityGraphQL;

/// <summary>
/// Used to indicate errors that occur during the processing of a GraphQL field so we can catch it and add
/// information about the field to the error.
/// </summary>
public sealed class EntityGraphQLFieldException : Exception
{
    public EntityGraphQLFieldException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
