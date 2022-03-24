using System.IO;
using System.Threading.Tasks;

namespace EntityGraphQL.AspNet
{
    /// <summary>
    /// Serializes GraphQL responses into a response format.
    /// </summary>
    public interface IGraphQLResponseSerializer
    {
        Task SerializeAsync<T>(Stream body, T data);
    }
}