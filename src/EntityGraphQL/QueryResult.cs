using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EntityGraphQL
{
    public class QueryResult
    {
        private List<GraphQLError>? errors = null;
        public ReadOnlyCollection<GraphQLError>? Errors => errors?.AsReadOnly();
        public ConcurrentDictionary<string, object?>? Data { get; set; } = null;

        public QueryResult() { }
        public QueryResult(GraphQLError error)
        {
            errors = new List<GraphQLError> { error };
        }
        public QueryResult(IEnumerable<GraphQLError> errors)
        {
            this.errors = errors.ToList();
        }

        public void AddError(GraphQLError error)
        {
            if (errors == null)
            {
                errors = new List<GraphQLError>();
            }

            errors.Add(error);
        }

        public void AddErrors(IEnumerable<GraphQLError> errors)
        {
            if (this.errors == null)
            {
                this.errors = new List<GraphQLError>();
            }

            this.errors.AddRange(errors);
        }
    }
}