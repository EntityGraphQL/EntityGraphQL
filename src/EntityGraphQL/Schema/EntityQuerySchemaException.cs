using System;

namespace EntityGraphQL.Schema;

public class EntityQuerySchemaException : Exception
{
    public EntityQuerySchemaException(string message)
        : base(message) { }

    public EntityQuerySchemaException(string message, Exception innerException)
        : base(message, innerException) { }
}
