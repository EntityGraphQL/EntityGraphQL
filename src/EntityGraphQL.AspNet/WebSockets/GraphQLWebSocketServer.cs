using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
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
public class GraphQLWebSocketServer<TQueryType> : IGraphQLWebSocketServer, IDisposable
{
    /// <summary>
    /// Active subscriptions keyed by operation id.
    /// ConcurrentDictionary because observer callbacks (OnNext/OnError/OnCompleted) can fire
    /// from threads other than the WebSocket receive loop.
    /// </summary>
    private readonly ConcurrentDictionary<string, IDisposable> subscriptions = new();

    /// <summary>
    /// All outbound WebSocket frames are written here and drained by a single background task,
    /// which guarantees WebSocket.SendAsync is never called concurrently (a .NET requirement).
    /// </summary>
    private readonly Channel<object> outgoing = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>
    /// Cancelled to stop the drain task when the connection closes (error or normal close).
    /// </summary>
    private readonly CancellationTokenSource closeCts = new();

    /// <summary>
    /// Serializes the WebSocket close so a protocol-level close (e.g. connection_init timeout) is not raced
    /// by the receive loop's cleanup close - the first close reason requested wins.
    /// </summary>
    private readonly SemaphoreSlim closeLock = new(1, 1);
    private WebSocketCloseStatus? pendingCloseStatus;
    private string? pendingCloseDescription;

    /// <summary>Represents the running drain task; awaited in HandleAsync's finally block.</summary>
    private Task drainTask = Task.CompletedTask;

    private readonly WebSocket webSocket;
    private readonly ExecutionOptions options;
    private readonly GraphQLWebSocketOptions webSocketOptions;
    private volatile bool initialized;
    private readonly JsonSerializerOptions jsonOptions = new() { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public HttpContext Context { get; }

    public GraphQLWebSocketServer(WebSocket webSocket, HttpContext context, ExecutionOptions? options = null, GraphQLWebSocketOptions? webSocketOptions = null)
    {
        this.webSocket = webSocket;
        this.Context = context;
        this.options = options ?? new ExecutionOptions();
        this.webSocketOptions = webSocketOptions ?? new GraphQLWebSocketOptions();
    }

    public async Task HandleAsync()
    {
        // Create a combined token: cancelled when the connection closes (via _closeCts) OR
        // the HTTP request is aborted (e.g. client disconnects at the transport layer).
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(Context.RequestAborted, closeCts.Token);
        drainTask = DrainOutgoingAsync(linked.Token);

        StartConnectionInitTimeout(linked.Token);

        try
        {
            while (!webSocket.CloseStatus.HasValue && webSocket.State == WebSocketState.Open)
            {
                using var memoryStream = new MemoryStream();
                WebSocketReceiveResult? receiveResult = null;
                var messageTooBig = false;
                do
                {
                    var buffer = new byte[1024 * 4];
                    var segment = new ArraySegment<byte>(buffer);
                    receiveResult = await webSocket.ReceiveAsync(segment, linked.Token);

                    if (receiveResult.CloseStatus.HasValue)
                    {
                        await CloseConnectionAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription);
                        break;
                    }

                    if (receiveResult.Count == 0)
                        continue;

                    if (webSocketOptions.MaxMessageSizeBytes.HasValue && memoryStream.Length + receiveResult.Count > webSocketOptions.MaxMessageSizeBytes.Value)
                    {
                        // stop accumulating an unbounded message into memory
                        await CloseConnectionAsync(WebSocketCloseStatus.MessageTooBig, "Message too large");
                        messageTooBig = true;
                        break;
                    }

                    await memoryStream.WriteAsync(segment.AsMemory(segment.Offset, receiveResult.Count), linked.Token);
                } while (!receiveResult.EndOfMessage);

                if (messageTooBig)
                    break;

                if (!webSocket.CloseStatus.HasValue)
                {
                    var message = Encoding.UTF8.GetString(memoryStream.ToArray());
                    GraphQLWSRequest? request;
                    try
                    {
                        request = JsonSerializer.Deserialize<GraphQLWSRequest>(message, jsonOptions);
                    }
                    catch (JsonException)
                    {
                        await CloseConnectionAsync((WebSocketCloseStatus)4400, "Invalid message - could not parse JSON");
                        break;
                    }
                    if (request == null)
                    {
                        await CloseConnectionAsync((WebSocketCloseStatus)4400, "Invalid message - could not parse JSON");
                        break;
                    }
                    await HandleMessageAsync(request);
                }
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Connection closed or HTTP request aborted — fall through to finally for cleanup.
        }
        finally
        {
            // Dispose subscriptions first so no new events can be enqueued after this point.
            DisposeAllSubscriptions();

            // Signal the drain task to stop (idempotent if CloseConnectionAsync already did it).
            closeCts.Cancel();
            outgoing.Writer.TryComplete();
            await drainTask;

            // Close the WebSocket if it isn't already closed.
            await CloseWebSocketAsync(webSocket.CloseStatus, webSocket.CloseStatusDescription);

            closeCts.Dispose();
        }
    }

    /// <summary>
    /// Per the graphql-ws protocol the client must send connection_init within a deadline or the server
    /// closes with 4408, so idle/misbehaving clients cannot hold sockets open indefinitely.
    /// </summary>
    private void StartConnectionInitTimeout(CancellationToken ct)
    {
        if (webSocketOptions.ConnectionInitTimeout == null)
            return;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(webSocketOptions.ConnectionInitTimeout.Value, ct);
                    if (!initialized)
                        await CloseConnectionAsync((WebSocketCloseStatus)4408, "Connection initialisation timeout");
                }
                catch (OperationCanceledException)
                {
                    // connection closed before the deadline - nothing to do
                }
            },
            CancellationToken.None
        );
    }

    /// <summary>
    /// Single reader that drains <see cref="outgoing"/> and writes frames to the WebSocket.
    /// Having exactly one sender guarantees we never call WebSocket.SendAsync concurrently.
    /// </summary>
    private async Task DrainOutgoingAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var msg in outgoing.Reader.ReadAllAsync(ct))
            {
                if (webSocket.State != WebSocketState.Open)
                    break;

                var json = JsonSerializer.Serialize(msg, jsonOptions);
                var buffer = Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
        }
        catch (OperationCanceledException)
        { /* connection closed or request aborted */
        }
        catch (WebSocketException)
        { /* socket closed mid-send */
        }
    }

    private async Task HandleMessageAsync(GraphQLWSRequest graphQLWSMessage)
    {
        switch (graphQLWSMessage.Type)
        {
            case GraphQLWSMessageType.ConnectionInit:
            {
                if (initialized)
                    await CloseConnectionAsync((WebSocketCloseStatus)4429, "Too many initialisation requests");
                else
                {
                    initialized = true;
                    Enqueue(new BaseGraphQLWSResponse { Type = GraphQLWSMessageType.ConnectionAck });
                }
                break;
            }
            case GraphQLWSMessageType.Ping:
                Enqueue(new BaseGraphQLWSResponse { Type = GraphQLWSMessageType.Pong });
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
        if (!initialized)
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
            {
                await CloseConnectionAsync((WebSocketCloseStatus)4409, $"Subscriber for {graphQLWSMessage.Id} already exists");
            }
            else
            {
                var request = graphQLWSMessage.Payload;
                // executing this sets up the observers etc. We don't return any data until we have an event
                var result = await schema.ExecuteRequestAsync(request, Context.RequestServices, Context.User, options, Context.RequestAborted)!;
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

                    // TryAdd is atomic: if two Subscribe messages race for the same id, the loser
                    // disposes its subscription cleanly rather than silently overwriting.
                    if (!subscriptions.TryAdd(graphQLWSMessage.Id, websocketSubscription))
                        websocketSubscription.Dispose();
                }
                else
                {
                    // Assume it is a query or mutation over websockets
                    if (result.Errors == null)
                    {
                        await SendNextAsync(graphQLWSMessage.Id, result);
                    }
                    // send complete after next or error above
                    Enqueue(new BaseWithIdGraphQLWSResponse { Type = GraphQLWSMessageType.Complete, Id = graphQLWSMessage.Id });
                }
            }
        }
    }

    public Task SendErrorAsync(string id, Exception exception)
    {
        return SendErrorAsync(id, new List<GraphQLError> { new(exception.Message, null) });
    }

    public Task SendErrorAsync(string id, IEnumerable<GraphQLError> errors)
    {
        Enqueue(
            new GraphQLWSError
            {
                Id = id,
                Type = GraphQLWSMessageType.Error,
                Payload = errors.ToList(),
            }
        );
        return Task.CompletedTask;
    }

    public Task CompleteSubscriptionAsync(string id)
    {
        if (subscriptions.TryRemove(id, out var subscription))
            subscription.Dispose();
        return Task.CompletedTask;
    }

    public Task SendNextAsync(string id, QueryResult result)
    {
        Enqueue(
            new GraphQLWSResponse
            {
                Id = id,
                Type = GraphQLWSMessageType.Next,
                Payload = result,
            }
        );
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues a message for the drain task to send. Never blocks.
    /// For an unbounded channel the only way <c>TryWrite</c> returns <c>false</c> is when
    /// the writer has already been completed — which happens intentionally during connection
    /// shutdown. Messages dropped at that point are expected and correct.
    /// </summary>
    private void Enqueue(object message) => outgoing.Writer.TryWrite(message);

    /// <summary>
    /// Handles protocol-level closes (errors, client-initiated close frames).
    /// Cancels the drain task so it releases the WebSocket exclusively before the close frame is sent.
    /// </summary>
    private async Task CloseConnectionAsync(WebSocketCloseStatus? closeStatus, string? closeStatusDescription)
    {
        // record the intended close reason - cancelling closeCts below wakes the receive loop whose cleanup
        // also closes the socket, and it must use this reason rather than racing in a normal closure
        if (pendingCloseStatus == null && closeStatus != null)
        {
            pendingCloseStatus = closeStatus;
            pendingCloseDescription = closeStatusDescription;
        }

        // Stop the drain task so no concurrent SendAsync races with the CloseAsync call below.
        closeCts.Cancel();
        outgoing.Writer.TryComplete();
        await drainTask;

        await CloseWebSocketAsync(closeStatus, closeStatusDescription);
    }

    private async Task CloseWebSocketAsync(WebSocketCloseStatus? closeStatus, string? closeStatusDescription)
    {
        await closeLock.WaitAsync();
        try
        {
            if (webSocket.State is not (WebSocketState.Closed or WebSocketState.CloseSent or WebSocketState.Aborted))
            {
                await webSocket.CloseAsync(closeStatus ?? pendingCloseStatus ?? WebSocketCloseStatus.NormalClosure, closeStatusDescription ?? pendingCloseDescription, CancellationToken.None);
            }
        }
        finally
        {
            closeLock.Release();
        }
    }

    private void DisposeAllSubscriptions()
    {
        foreach (var key in subscriptions.Keys)
        {
            if (subscriptions.TryRemove(key, out var sub))
                sub.Dispose();
        }
    }

    public void Dispose()
    {
        closeCts.Dispose();
        GC.SuppressFinalize(this);
    }
}
