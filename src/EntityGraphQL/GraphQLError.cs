using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL;

/// <summary>
/// Describes any errors that might happen while resolving the query request
/// </summary>
public class GraphQLError : Dictionary<string, object>
{
    private static readonly string MessageKey = "message";
    private static readonly string PathKey = "path";

    public string Message => (string)this[MessageKey];

    public List<string>? Path
    {
        get => (TryGetValue(PathKey, out var value) && value is List<string> list) ? list : null;
        set
        {
            if (value == null)
                Remove(PathKey);
            else
                this[PathKey] = value;
        }
    }

    public Dictionary<string, object>? Extensions => (Dictionary<string, object>?)this.GetValueOrDefault(QueryResult.ExtensionsKey);

    public GraphQLError(string message, IDictionary<string, object>? extensions)
    {
        this[MessageKey] = message;
        if (extensions != null)
            this[QueryResult.ExtensionsKey] = new Dictionary<string, object>(extensions);
    }

    public bool IsExecutionError => Path != null;

    public GraphQLError(string message, IEnumerable<string>? path, IDictionary<string, object>? extensions)
    {
        this[MessageKey] = message;
        if (path != null)
            this[PathKey] = path.ToList();
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
