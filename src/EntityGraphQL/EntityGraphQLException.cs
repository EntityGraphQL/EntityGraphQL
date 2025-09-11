using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL;

/// <summary>
/// Represents errors that occur during the execution of a GraphQL query.
/// Available for users to throw and add additional metadata.
/// </summary>
public class EntityGraphQLException : Exception
{
    public HashSet<string> Messages { get; }
    public Dictionary<string, object> Extensions { get; } = new();
    public GraphQLErrorCategory Category { get; }
    public List<string> Path { get; set; } = new();

    public EntityGraphQLException(string message, IDictionary<string, object>? extensions = null, Exception? innerException = null)
        : this(GraphQLErrorCategory.ExecutionError, [message], extensions, null, innerException) { }

    public EntityGraphQLException(GraphQLErrorCategory category, string message, IDictionary<string, object>? extensions = null, IEnumerable<string>? path = null, Exception? innerException = null)
        : this(category, [message], extensions, path, innerException) { }

    public EntityGraphQLException(
        GraphQLErrorCategory category,
        IEnumerable<string> messages,
        IDictionary<string, object>? extensions = null,
        IEnumerable<string>? path = null,
        Exception? innerException = null
    )
        : base(messages.First(), innerException)
    {
        Category = category;
        Messages = messages.ToHashSet();
        if (path != null)
            Path = path.ToList();
        if (extensions != null)
            Extensions = new Dictionary<string, object>(extensions.ToDictionary(kv => kv.Key, kv => kv.Value));
    }
}

public enum GraphQLErrorCategory
{
    /// <summary>
    /// Document parsing/validation - should return 200
    /// </summary>
    DocumentError,

    /// <summary>
    /// Field execution errors - should return 200
    /// </summary>
    ExecutionError,
}
