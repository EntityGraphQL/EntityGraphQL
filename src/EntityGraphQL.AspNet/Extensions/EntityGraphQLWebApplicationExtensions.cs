using System.Net.WebSockets;
using EntityGraphQL.Schema;
using EntityGraphQL.AspNet.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace EntityGraphQL.AspNet
{
    public static class EntityGraphQLWebApplicationExtensions
    {
        public static IApplicationBuilder UseGraphQLWebSockets<TQueryType>(this IApplicationBuilder app, string path = "/subscriptions", ExecutionOptions? options = null)
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
#if NET3_1 || NET5
                    await next();
#elif NET6_0_OR_GREATER
                    await next(context);
#endif
                }
            });

            return app;
        }
    }
}