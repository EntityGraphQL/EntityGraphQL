using System.Collections.Generic;

namespace EntityGraphQL
{
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

        public Dictionary<string, Dictionary<string, object>> Extensions { get; set; } = new Dictionary<string, Dictionary<string, object>>();
    }

    public class PersistedQueryExtension : Dictionary<string, object>
    {
        public PersistedQueryExtension()
        {
            Version = 1;
        }
        public string Sha256Hash { get => (string)this[nameof(Sha256Hash)]; set => this[nameof(Sha256Hash)] = value; }

        public int Version { get => (int)this[nameof(Version)]; set => this[nameof(Version)] = value; }
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

        public string Message { get => (string)this[MessageKey]; }

        public Dictionary<string, object>? Extensions { get => (Dictionary<string, object>?)this.GetValueOrDefault(QueryResult.ExtensionsKey); }

        public GraphQLError(string message, IDictionary<string, object>? extensions)
        {
            this[MessageKey] = message;
            if (extensions != null)
                this[QueryResult.ExtensionsKey] = new Dictionary<string, object>(extensions);
        }
    }
}