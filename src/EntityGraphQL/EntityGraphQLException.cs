using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EntityGraphQL;

public class EntityGraphQLException : Exception
{
    public ReadOnlyDictionary<string, object> Extensions { get; }

    public EntityGraphQLException(string message, IDictionary<string, object> extensions) : base(message)
    {
        Extensions = new ReadOnlyDictionary<string, object>(extensions);
    }
    public EntityGraphQLException(string fieldName, EntityGraphQLException ex) : base($"Field '{fieldName}' - {ex.Message}")
    {
        Extensions = ex.Extensions;
    }
}