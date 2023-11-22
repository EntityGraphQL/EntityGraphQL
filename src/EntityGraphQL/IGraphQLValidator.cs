using System.Collections.Generic;

namespace EntityGraphQL
{
    public interface IGraphQLValidator
    {
        List<GraphQLError> Errors { get; }
        bool HasErrors { get; }

        void AddError(string message);
        void AddError(string message, Dictionary<string, object> extensions);
    }
}