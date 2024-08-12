using System.Collections.Generic;

namespace EntityGraphQL
{
    public class GraphQLValidator : IGraphQLValidator
    {
        public List<GraphQLError> Errors { get; set; } = new List<GraphQLError>();
        public bool HasErrors => Errors.Count > 0;

        public void AddError(string message) => Errors.Add(new GraphQLError(message, null));

        public void AddError(string message, Dictionary<string, object> extensions) => Errors.Add(new GraphQLError(message, extensions));
    }
}
