using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL
{
    public class QueryResult : Dictionary<string, object>
    {
        private static readonly string DataKey = "data";
        private static readonly string ErrorsKey = "errors";
        internal static readonly string ExtensionsKey = "extensions";
        public List<GraphQLError>? Errors => (List<GraphQLError>?)this.GetValueOrDefault(ErrorsKey);
        public Dictionary<string, object?>? Data { get => (Dictionary<string, object?>?)this.GetValueOrDefault(DataKey); }
        /// <summary>
        /// Use Extensions to add any custom data for the result
        /// </summary>
        public Dictionary<string, object>? Extensions { get => (Dictionary<string, object>?)this.GetValueOrDefault(ExtensionsKey); }

        public QueryResult() { }
        public QueryResult(GraphQLError error)
        {
            this[ErrorsKey] = new List<GraphQLError> { error };
        }
        public QueryResult(IEnumerable<GraphQLError> errors)
        {
            this[ErrorsKey] = errors.ToList();
        }
        public bool HasErrors() => Errors?.Count > 0;

        public void AddError(string error, IDictionary<string, object>? extensions = null)
        {
            AddError(new GraphQLError(error, extensions));
        }

        public void AddError(GraphQLError error)
        {
            if (!this.ContainsKey(ErrorsKey))
            {
                this[ErrorsKey] = new List<GraphQLError>();
            }

            ((List<GraphQLError>)this[ErrorsKey]).Add(error);
        }

        public void AddErrors(IEnumerable<GraphQLError> errors)
        {
            if (!this.ContainsKey(ErrorsKey))
            {
                this[ErrorsKey] = new List<GraphQLError>();
            }
            ((List<GraphQLError>)this[ErrorsKey]).AddRange(errors);
        }

        public void SetData(IDictionary<string, object?> data)
        {
            this[DataKey] = data.ToDictionary(d => d.Key, d => d.Value);
        }
    }
}