using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL
{
    public class GraphQLValidator : IGraphQLValidator
    {
        public List<GraphQLError> Errors { get; set; } = new List<GraphQLError>();
        public bool HasErrors => Errors.Any();

        public void AddError(string message) => Errors.Add(new GraphQLError(message, null));
        public void AddError(string message, Dictionary<string, object> extensions) => Errors.Add(new GraphQLError(message, extensions));
    }
}