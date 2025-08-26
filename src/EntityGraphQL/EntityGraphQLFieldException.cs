using System;
using EntityGraphQL.Compiler;

namespace EntityGraphQL;

internal sealed class EntityGraphQLFieldException : Exception
{
    public readonly string FieldName;
    public readonly string[]? Path;

    public EntityGraphQLFieldException(string fieldName, string[]? path, Exception innerException)
        : base(null, innerException)
    {
        FieldName = fieldName;
        Path = path;
    }
}
