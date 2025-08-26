using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL;

/// <summary>
/// A GraphQL request. The query, and any variables
/// </summary>
public class QueryRequest
{
    // Name of the operation you want to run in the Query document (if it contains many)
    public string? OperationName { get; set; }

    /// <summary>
    /// GraphQL query document
    /// </summary>
    /// <value></value>
    public string? Query { get; set; }

    /// <summary>
    /// Variables values for the query.
    /// It is a Dictionary<string, object?> type. Make sure that object is a resolved .NET type and
    /// not a JsonElement/JObject (e.g. turn objects into more dictionaries)
    /// </summary>
    public QueryVariables? Variables { get; set; }

    public Dictionary<string, Dictionary<string, object>> Extensions { get; set; } = [];
}

public class QueryExtensions : Dictionary<string, Dictionary<string, object>>
{
    public QueryExtensions() { }

    public QueryExtensions(IDictionary<string, Dictionary<string, object>> dictionary)
        : base(dictionary) { }
}

public class PersistedQueryExtension : Dictionary<string, object>
{
    public PersistedQueryExtension()
    {
        Version = 1;
    }

    public string Sha256Hash
    {
        get => (string)this[nameof(Sha256Hash)];
        set => this[nameof(Sha256Hash)] = value;
    }

    public int Version
    {
        get => (int)this[nameof(Version)];
        set => this[nameof(Version)] = value;
    }
}

/// <summary>
/// Holds the variables passed along with a GraphQL query
/// </summary>
public class QueryVariables : Dictionary<string, object?>
{
    public object? GetValueFor(string varKey)
    {
        return ContainsKey(varKey) ? this[varKey] : null;
    }
}

/// <summary>
/// Describes any errors that might happen while resolving the query request
/// </summary>
public class GraphQLError : Dictionary<string, object>
{
    private static readonly string MessageKey = "message";
    private static readonly string PathKey = "path";

    public string Message => (string)this[MessageKey];

    public string[] Path
    {
        get => (string[])this[PathKey];
        set => this[PathKey] = value;
    }

    public Dictionary<string, object>? Extensions => (Dictionary<string, object>?)this.GetValueOrDefault(QueryResult.ExtensionsKey);

    public GraphQLError(string message, IDictionary<string, object>? extensions)
    {
        this[MessageKey] = message;
        this[PathKey] = (string[])[];
        if (extensions != null)
            this[QueryResult.ExtensionsKey] = new Dictionary<string, object>(extensions);
    }

    public GraphQLError(string message, string[]? path, IDictionary<string, object>? extensions)
    {
        this[MessageKey] = message;
        this[PathKey] = path ?? [];
        if (extensions != null)
            this[QueryResult.ExtensionsKey] = new Dictionary<string, object>(extensions);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GraphQLError other)
            return false;

        bool extensionsEqual =
            (Extensions == null && other.Extensions == null)
            || (Extensions != null && other.Extensions != null && Extensions.Count == other.Extensions.Count && !Extensions.Except(other.Extensions).Any());

        return Message == other.Message && ((Path == null && other.Path == null) || (Path != null && other.Path != null && Path.SequenceEqual(other.Path))) && extensionsEqual;
    }

    public override int GetHashCode()
    {
        int hash = Message?.GetHashCode() ?? 0;
        if (Path != null)
        {
            foreach (var p in Path)
                hash = hash * 31 + (p?.GetHashCode() ?? 0);
        }
        if (Extensions != null)
        {
            foreach (var kv in Extensions.OrderBy(kv => kv.Key))
            {
                hash = hash * 31 + kv.Key.GetHashCode();
                hash = hash * 31 + (kv.Value?.GetHashCode() ?? 0);
            }
        }
        return hash;
    }
}
