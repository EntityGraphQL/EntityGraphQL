using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL
{
    public class GraphQLValidator
    {
        public List<GraphQLError> Errors { get; set; } = new List<GraphQLError>();
        public bool HasErrors => Errors.Any();

        public void AddError(string error) => Errors.Add(new GraphQLError(error, null));
        public void AddError(string error, Dictionary<string, object> extensions) => Errors.Add(new GraphQLError(error, extensions));
    }
}