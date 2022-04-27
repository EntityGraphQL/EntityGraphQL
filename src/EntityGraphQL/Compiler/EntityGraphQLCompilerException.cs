using System;
using System.Reflection;

namespace EntityGraphQL.Compiler;

public class EntityGraphQLCompilerException : Exception
{
    public EntityGraphQLCompilerException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}

public class EntityGraphQLExecutionException : Exception
{
    public EntityGraphQLExecutionException(string message) : base(message)
    {
    }
    public EntityGraphQLExecutionException(string field, Exception innerException)
        : base($"Field '{field}' - {innerException.Message}", innerException)
    {
    }
}
public class EntityGraphQLAccessException : Exception
{
    public EntityGraphQLAccessException(string message) : base(message)
    {
    }
}
