using System.IO;
using System.Threading.Tasks;

namespace EntityGraphQL.AspNet
{
    /// <summary>
    /// Deserializes GraphQL requests into a QueryRequest object.
    /// </summary>
    public interface IGraphQLRequestDeserializer
    {
        Task<QueryRequest> DeserializeAsync(Stream body);
    }
}