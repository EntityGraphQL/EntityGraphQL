using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace EntityGraphQL.AspNet.WebSockets;

public interface IGraphQLWebSocketServer
{
    public HttpContext Context { get; }
    void CompleteSubscription(Guid id);
    Task SendErrorAsync(Guid id, Exception exception);
    Task SendNextAsync(Guid id, QueryResult result);
}
