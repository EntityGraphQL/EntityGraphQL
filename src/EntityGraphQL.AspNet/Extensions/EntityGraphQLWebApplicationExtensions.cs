using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using EntityGraphQL.Schema;
using EntityGraphQL.AspNet.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EntityGraphQL.AspNet
{
    public static class EntityGraphQLWebApplicationExtensions
    {
#if NET6
    public static WebApplication UseGraphQLWebSockets<TQueryType>(this WebApplication app, string path = "/subscriptions", ExecutionOptions? options = null)
    {
        path = path.TrimEnd('/');

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == path)
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync("graphql-transport-ws");
                    if (!context.WebSockets.WebSocketRequestedProtocols.Contains(webSocket.SubProtocol!))
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError,
                            "Server only supports the graphql-ws protocol",
                            context.RequestAborted);
                        
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    var server = new GraphQLWebSocketServer<TQueryType>(webSocket, context);

                    await server.HandleAsync();
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                await next(context);
            }
        });

        return app;
    }
#endif
    }
}