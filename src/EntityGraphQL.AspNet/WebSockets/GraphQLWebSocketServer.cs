using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using EntityGraphQL.Subscriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.AspNet.WebSockets;

/// <summary>
/// Implementation of the GraphQL over WebSocket protocol - https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md.
/// </summary>
/// <typeparam name="TQueryType"></typeparam>
public class GraphQLWebSocketServer<TQueryType> : IGraphQLWebSocketServer
{
    /// <summary>
    /// These are the subscriptions/clients that are currently active with this server.
    /// </summary>
    private readonly Dictionary<string, IDisposable> subscriptions = [];
    private readonly WebSocket webSocket;
    private readonly ExecutionOptions options;
    private bool initialised;
    private readonly JsonSerializerOptions jsonOptions = new() { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public HttpContext Context { get; }

    public GraphQLWebSocketServer(WebSocket webSocket, HttpContext context, ExecutionOptions? options = null)
    {
        this.webSocket = webSocket;
        this.Context = context;
        this.options = options ?? new ExecutionOptions();
    }

    public async Task HandleAsync()
    {
        while (!webSocket.CloseStatus.HasValue && webSocket.State == WebSocketState.Open)
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

        await CloseConnectionAsync(webSocket.CloseStatus, webSocket.CloseStatusDescription);
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
            case GraphQLWSMessageType.Ping:
                await SendSimpleResponseAsync(GraphQLWSMessageType.Pong);
                break;
            case GraphQLWSMessageType.Subscribe:
                await HandleSubscribeAsync(graphQLWSMessage);
                break;
            case GraphQLWSMessageType.Complete:
                if (graphQLWSMessage.Id == null)
                    await CloseConnectionAsync((WebSocketCloseStatus)4400, "Invalid complete message, missing id field.");
                else
                    await CompleteSubscriptionAsync(graphQLWSMessage.Id);
                break;
            case GraphQLWSMessageType.Pong:
                break; // can come to us but we don't care
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

        if (graphQLWSMessage.Id == null)
            await CloseConnectionAsync((WebSocketCloseStatus)4400, "Invalid subscribe message, missing id field.");
        else if (graphQLWSMessage.Payload == null)
            await CloseConnectionAsync((WebSocketCloseStatus)4400, "Invalid subscribe message, missing payload field.");
        else
        {
            var schema = Context.RequestServices.GetService<SchemaProvider<TQueryType>>();
            if (schema == null)
            {
                await CloseConnectionAsync(
                    (WebSocketCloseStatus)4400,
                    $"No SchemaProvider<{typeof(TQueryType).Name}> found in the service collection. Make sure you set up your Startup.ConfigureServices() to call AddGraphQLSchema<{typeof(TQueryType).Name}>()."
                );
                return;
            }

            if (subscriptions.ContainsKey(graphQLWSMessage.Id))
                await CloseConnectionAsync((WebSocketCloseStatus)4409, $"Subscriber for {graphQLWSMessage.Id} already exists");
            else
            {
                var request = graphQLWSMessage.Payload;
                // executing this sets up the observers etc. We don't return any data until we have an event
                var result = await schema.ExecuteRequestAsync(request, Context.RequestServices, Context.User, options)!;
                if (result.Errors != null)
                {
                    await SendErrorAsync(graphQLWSMessage.Id, result.Errors);
                }
                // No error and a successful subscribe operation
                if (result.Data?.Values.First() is GraphQLSubscribeResult subscribeResult)
                {
                    var websocketSubscription = (IDisposable)
                        Activator.CreateInstance(
                            typeof(WebSocketSubscription<,>).MakeGenericType(typeof(TQueryType), subscribeResult!.EventType),
                            graphQLWSMessage.Id,
                            subscribeResult!.GetObservable(),
                            this,
                            subscribeResult!.SubscriptionStatement,
                            subscribeResult!.Field
                        )!;
                    subscriptions.Add(graphQLWSMessage.Id, websocketSubscription);
                }
                else
                {
                    // Assume it is a query or mutation over websockets
                    if (result.Errors == null)
                    {
                        await SendNextAsync(graphQLWSMessage.Id, result);
                    }
                    // send complete after next or error above
                    await SendAsync(new BaseWithIdGraphQLWSResponse { Type = GraphQLWSMessageType.Complete, Id = graphQLWSMessage.Id });
                }
            }
        }
    }

    public async Task SendErrorAsync(string id, Exception exception)
    {
        await SendErrorAsync(id, new List<GraphQLError> { new(exception.Message, null) });
    }

    public Task SendErrorAsync(string id, IEnumerable<GraphQLError> errors)
    {
        return SendAsync(
            new GraphQLWSError
            {
                Id = id,
                Type = GraphQLWSMessageType.Error,
                Payload = errors.ToList(),
            }
        );
    }

    public Task CompleteSubscriptionAsync(string id)
    {
        subscriptions.TryGetValue(id, out var subscription);
        if (subscription != null)
        {
            subscription.Dispose();
            subscriptions.Remove(id);
        }
        return Task.CompletedTask;
    }

    public async Task SendNextAsync(string id, QueryResult result)
    {
        await SendAsync(
            new GraphQLWSResponse
            {
                Id = id,
                Type = GraphQLWSMessageType.Next,
                Payload = result,
            }
        );
    }

    private async Task SendSimpleResponseAsync(string type)
    {
        await SendAsync(new BaseGraphQLWSResponse { Type = type });
    }

    private Task SendAsync(object graphQLWSMessage)
    {
        if (webSocket.State != WebSocketState.Open)
            return Task.CompletedTask;
        var json = JsonSerializer.Serialize(graphQLWSMessage, jsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(buffer);
        return webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task CloseConnectionAsync(WebSocketCloseStatus? closeStatus, string? closeStatusDescription)
    {
        if (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.CloseSent && webSocket.State != WebSocketState.Aborted)
        {
            await webSocket.CloseAsync(closeStatus ?? WebSocketCloseStatus.NormalClosure, closeStatusDescription, CancellationToken.None);
        }

        foreach (var subscription in subscriptions.Values)
            subscription.Dispose();
    }
}
