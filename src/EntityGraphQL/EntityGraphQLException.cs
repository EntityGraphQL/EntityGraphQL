using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EntityGraphQL;

public class EntityGraphQLException : Exception
{
    public IReadOnlyDictionary<string, object> Extensions { get; }

    public EntityGraphQLException(string message) : base(message)
    {
        Extensions = new Dictionary<string, object>();
    }
    public EntityGraphQLException(string message, IDictionary<string, object> extensions) : base(message)
    {
        Extensions = new ReadOnlyDictionary<string, object>(extensions);
    }
}