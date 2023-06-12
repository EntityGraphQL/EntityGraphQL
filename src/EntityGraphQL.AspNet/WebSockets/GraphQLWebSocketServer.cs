using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using EntityGraphQL.Subscriptions;

namespace EntityGraphQL.AspNet.WebSockets
{
    /// <summary>
    /// Implementation of the GraphQL over WebSocket protocol - https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md.
    /// </summary>
    /// <typeparam name="TQueryType"></typeparam>
    public class GraphQLWebSocketServer<TQueryType> : IGraphQLWebSocketServer
    {
        /// <summary>
        /// These are the subscriptions/clients that are currently active with this server.
        /// </summary>
        private readonly Dictionary<Guid, IDisposable> subscriptions = new();
        private readonly WebSocket webSocket;
        private readonly HttpContext context;
        private readonly ExecutionOptions options;
        private bool initialised;
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            IncludeFields = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public GraphQLWebSocketServer(WebSocket webSocket, HttpContext context, ExecutionOptions? options = null)
        {
            this.webSocket = webSocket;
            this.context = context;
            this.options = options ?? new ExecutionOptions();
        }

        public async Task HandleAsync()
        {
            while (!webSocket.CloseStatus.HasValue)
            {
                using var memoryStream = new MemoryStream();
                WebSocketReceiveResult? receiveResult = null;
                do
                {
                    var buffer = new byte[1024 * 4];
                    var segment = new ArraySegment<byte>(buffer);
                    receiveResult = await webSocket.ReceiveAsync(segment, CancellationToken.None);

                    if (receiveResult.CloseStatus.HasValue)
                    {
                        await CloseConnectionAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription);
                        break;
                    }

                    if (receiveResult.Count == 0)
                        continue;

                    await memoryStream.WriteAsync(segment.AsMemory(segment.Offset, receiveResult.Count), CancellationToken.None);
                } while (!receiveResult.EndOfMessage);

                if (!webSocket.CloseStatus.HasValue)
                {
                    var message = Encoding.UTF8.GetString(memoryStream.ToArray());
                    await HandleMessageAsync(JsonSerializer.Deserialize<GraphQLWSRequest>(message, jsonOptions)!);
                }
            }

            await CloseConnectionAsync(webSocket.CloseStatus.Value, webSocket.CloseStatusDescription);
        }

        private async Task HandleMessageAsync(GraphQLWSRequest graphQLWSMessage)
        {
            switch (graphQLWSMessage.Type)
            {
                case GraphQLWSMessageType.ConnectionInit:
                    {
                        if (initialised)
                            await CloseConnectionAsync((WebSocketCloseStatus)4429, "Too many initialisation requests");
                        else
                        {
                            initialised = true;
                            await SendSimpleResponseAsync(GraphQLWSMessageType.ConnectionAck);
                        }
                        break;
                    }
                case GraphQLWSMessageType.Ping: await SendSimpleResponseAsync(GraphQLWSMessageType.Pong); break;
                case GraphQLWSMessageType.Subscribe: await HandleSubscribeAsync(graphQLWSMessage); break;
                case GraphQLWSMessageType.Complete: CompleteSubscription(graphQLWSMessage.Id!.Value); break;
                case GraphQLWSMessageType.Pong: break; // can come to us but we don't care
                default:
                    await CloseConnectionAsync(WebSocketCloseStatus.InvalidMessageType, $"Unknown message type: {graphQLWSMessage.Type}");
                    break;
            }
        }

        private async Task HandleSubscribeAsync(GraphQLWSRequest graphQLWSMessage)
        {
            if (!initialised)
            {
                await CloseConnectionAsync((WebSocketCloseStatus)4401, "Unauthorized");
                return;
            }

            if (!graphQLWSMessage.Id.HasValue)
                await CloseConnectionAsync((WebSocketCloseStatus)4400, "Invalid subscribe message, missing id field.");
            else if (graphQLWSMessage.Payload == null)
                await CloseConnectionAsync((WebSocketCloseStatus)4400, "Invalid subscribe message, missing payload field.");
            else
            {
                var schema = context.RequestServices.GetService<SchemaProvider<TQueryType>>();
                if (schema == null)
                {
                    await CloseConnectionAsync((WebSocketCloseStatus)4400, $"No SchemaProvider<{typeof(TQueryType).Name}> found in the service collection. Make sure you set up your Startup.ConfigureServices() to call AddGraphQLSchema<{typeof(TQueryType).Name}>().");
                    return;
                }

                var schemaContext = context.RequestServices.GetService<TQueryType>();
                if (schemaContext == null)
                    await CloseConnectionAsync((WebSocketCloseStatus)4400, $"No schema context was found in the service collection. Make sure the {typeof(TQueryType).Name} used with MapGraphQL<{typeof(TQueryType).Name}>() is registered in the service collection.");
                else if (subscriptions.ContainsKey(graphQLWSMessage.Id.Value))
                    await CloseConnectionAsync((WebSocketCloseStatus)4409, $"Subscriber for {graphQLWSMessage.Id.Value} already exists");
                else
                {
                    var request = graphQLWSMessage.Payload;
                    // executing this sets up the observers etc. We don't return any data until we have an event
                    var result = await schema.ExecuteRequestWithContextAsync(request, schemaContext, context.RequestServices, context.User, options)!;
                    if (result.Errors != null)
                    {
                        await SendErrorAsync(graphQLWSMessage.Id!.Value, result.Errors);
                    }
                    // No error and a successful subscribe operation
                    if (result.Data?.Values.First() is GraphQLSubscribeResult subscribeResult)
                    {
                        var websocketSubscription = (IDisposable)Activator.CreateInstance(typeof(WebSocketSubscription<,>).MakeGenericType(typeof(TQueryType), subscribeResult!.EventType), graphQLWSMessage.Id!.Value, subscribeResult!.GetObservable(), this, subscribeResult!.SubscriptionStatement, subscribeResult!.Field)!;
                        subscriptions.Add(graphQLWSMessage.Id!.Value, websocketSubscription);
                    }
                    else
                    {
                        // Assume it is a query or mutation over websockets
                        if (result.Errors == null)
                        {
                            await SendNextAsync(graphQLWSMessage.Id!.Value, result);
                        }
                        // send complete after next or error above
                        await SendAsync(new WithIdGraphQLWSResponse
                        {
                            Type = GraphQLWSMessageType.Complete,
                            Id = graphQLWSMessage.Id!.Value,
                        });
                    }
                }
            }
        }

        public async Task SendErrorAsync(Guid id, Exception exception)
        {
            await SendErrorAsync(id, new List<GraphQLError> { new GraphQLError(exception.Message, null) });
        }
        public Task SendErrorAsync(Guid id, IEnumerable<GraphQLError> errors)
        {
            return SendAsync(new GraphQLWSError
            {
                Id = id,
                Type = GraphQLWSMessageType.Error,
                Payload = errors.ToList(),
            });
        }

        public void CompleteSubscription(Guid id)
        {
            subscriptions.TryGetValue(id, out var subscription);
            if (subscription != null)
            {
                subscription.Dispose();
                subscriptions.Remove(id);
            }
        }

        public async Task SendNextAsync(Guid id, QueryResult result)
        {
            await SendAsync(new GraphQLWSResponse
            {
                Id = id,
                Type = GraphQLWSMessageType.Next,
                Payload = result,
            });
        }

        private async Task SendSimpleResponseAsync(string type)
        {
            await SendAsync(new TypeOnlyGraphQLWSResponse
            {
                Type = type
            });
        }

        private Task SendAsync(object graphQLWSMessage)
        {
            var json = JsonSerializer.Serialize(graphQLWSMessage, jsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(buffer);
            return webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task CloseConnectionAsync(WebSocketCloseStatus closeStatus, string? closeStatusDescription)
        {
            if (webSocket.State != WebSocketState.Closed &&
                webSocket.State != WebSocketState.CloseSent &&
                webSocket.State != WebSocketState.Aborted)
            {
                await webSocket.CloseAsync(closeStatus, closeStatusDescription, CancellationToken.None);
            }

            foreach (var subscription in subscriptions.Values)
                subscription.Dispose();
        }
    }

    public interface IGraphQLWebSocketServer
    {
        void CompleteSubscription(Guid id);
        Task SendErrorAsync(Guid id, Exception exception);
        Task SendNextAsync(Guid id, QueryResult result);
    }
}