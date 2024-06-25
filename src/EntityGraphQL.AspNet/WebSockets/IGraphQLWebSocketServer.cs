using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace EntityGraphQL.AspNet.WebSockets;

public interface IGraphQLWebSocketServer
{
    public HttpContext Context { get; }
    Task CompleteSubscriptionAsync(string id);
    Task SendErrorAsync(string id, Exception exception);
    Task SendNextAsync(string id, QueryResult result);
}
