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
    private static readonly string LocationsKey = "locations";

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

    public List<GraphQLSourceLocation>? Locations
    {
        get => (TryGetValue(LocationsKey, out var value) && value is List<GraphQLSourceLocation> list) ? list : null;
        set
        {
            if (value == null)
                Remove(LocationsKey);
            else
                this[LocationsKey] = value;
        }
    }

    public Dictionary<string, object>? Extensions => (Dictionary<string, object>?)this.GetValueOrDefault(QueryResult.ExtensionsKey);

    public GraphQLError(string message, IDictionary<string, object>? extensions, IEnumerable<GraphQLSourceLocation>? locations = null)
    {
        this[MessageKey] = message;
        if (locations != null)
            this[LocationsKey] = locations.ToList();
        if (extensions != null)
            this[QueryResult.ExtensionsKey] = new Dictionary<string, object>(extensions);
    }

    public bool IsExecutionError => Path != null;

    public GraphQLError(string message, IEnumerable<string>? path, IDictionary<string, object>? extensions, IEnumerable<GraphQLSourceLocation>? locations = null)
    {
        this[MessageKey] = message;
        if (path != null)
            this[PathKey] = path.ToList();
        if (locations != null)
            this[LocationsKey] = locations.ToList();
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

        bool locationsEqual = (Locations == null && other.Locations == null) || (Locations != null && other.Locations != null && Locations.SequenceEqual(other.Locations));

        return Message == other.Message && ((Path == null && other.Path == null) || (Path != null && other.Path != null && Path.SequenceEqual(other.Path))) && locationsEqual && extensionsEqual;
    }

    public override int GetHashCode()
    {
        int hash = Message?.GetHashCode() ?? 0;
        if (Path != null)
        {
            foreach (var p in Path)
                hash = hash * 31 + (p?.GetHashCode() ?? 0);
        }
        if (Locations != null)
        {
            foreach (var location in Locations)
                hash = hash * 31 + location.GetHashCode();
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
