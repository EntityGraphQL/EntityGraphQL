using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet.WebSockets;

/// <summary>
/// Ties the GraphQL subscription to the WebSocket connection.
///
/// Events are queued and processed by a single background consumer per subscription so the publisher's
/// OnNext never blocks on executing the (potentially async/service-backed) result selection, while events
/// for one subscription are still delivered in order.
/// </summary>
/// <typeparam name="TQueryContext">The main GraphQL query context type</typeparam>
/// <typeparam name="TEventType">The type of the subscription result</typeparam>
public sealed class WebSocketSubscription<TQueryContext, TEventType> : IDisposable, IObserver<TEventType>
{
    /// <summary>
    /// unique-operation-id from the protocol
    /// </summary>
    public string OperationId { get; }
    private readonly IObservable<TEventType> observable;
    private readonly IGraphQLWebSocketServer server;
    private readonly IDisposable subscription;
    private readonly GraphQLSubscriptionStatement subscriptionStatement;
    private readonly GraphQLSubscriptionField subscriptionNode;
    private int _disposed;

    /// <summary>
    /// Ordered event queue. Error/completion travel through the same channel as events so they can not
    /// overtake events queued before them (matching the Rx ordering contract).
    /// </summary>
    private readonly Channel<(TEventType? Value, Exception? Error, bool Completed)> events = Channel.CreateUnbounded<(TEventType?, Exception?, bool)>(
        new UnboundedChannelOptions { SingleReader = true }
    );

    public WebSocketSubscription(string id, object observable, IGraphQLWebSocketServer server, GraphQLSubscriptionStatement subscriptionStatement, GraphQLSubscriptionField node)
    {
        this.OperationId = id;
        if (observable is not IObservable<TEventType>)
            throw new ArgumentException($"{nameof(observable)} must be of type {nameof(IObservable<TEventType>)}");
        this.observable = (IObservable<TEventType>)observable;
        this.server = server;
        this.subscriptionStatement = subscriptionStatement;
        this.subscriptionNode = node;

        _ = Task.Run(ProcessEventsAsync);
        this.subscription = this.observable.Subscribe(this);
    }

    public void OnNext(TEventType value)
    {
        // never blocks - the background consumer executes the selection and enqueues the result
        events.Writer.TryWrite((value, null, false));
    }

    public void OnError(Exception error)
    {
        // Per the Rx contract, OnError means the sequence has terminated - but it must be delivered after
        // any events already queued, so it goes through the queue too.
        events.Writer.TryWrite((default, error, false));
        events.Writer.TryComplete();
    }

    public void OnCompleted()
    {
        events.Writer.TryWrite((default, null, true));
        events.Writer.TryComplete();
    }

    private async Task ProcessEventsAsync()
    {
        await foreach (var (value, error, completed) in events.Reader.ReadAllAsync())
        {
            if (Volatile.Read(ref _disposed) == 1)
                return;

            if (error != null)
            {
                // SendErrorAsync only enqueues - the connection's drain task delivers it.
                await server.SendErrorAsync(OperationId, error);
                // Remove the subscription from the server so no further events are processed.
                await server.CompleteSubscriptionAsync(OperationId);
                return;
            }
            if (completed)
            {
                // The observable sequence has ended normally; clean up the server-side subscription.
                await server.CompleteSubscriptionAsync(OperationId);
                return;
            }

            try
            {
                var data = await subscriptionStatement.ExecuteSubscriptionEventAsync<TQueryContext, TEventType>(
                    subscriptionNode,
                    value!,
                    server.Context.RequestServices,
                    new QueryRequestContext(subscriptionStatement.Schema.AuthorizationService, server.Context.User)
                );
                var result = new QueryResult();
                result.SetData(new Dictionary<string, object?> { { subscriptionNode.Name, data } });
                // SendNextAsync only enqueues - never blocks, never throws.
                await server.SendNextAsync(OperationId, result);
            }
            catch (Exception ex)
            {
                // an error executing the selection terminates this subscription per the protocol
                await server.SendErrorAsync(OperationId, ex);
                await server.CompleteSubscriptionAsync(OperationId);
                return;
            }
        }
    }

    public void Dispose()
    {
        // Interlocked ensures the inner subscription is disposed exactly once even if
        // CompleteSubscriptionAsync and an explicit Dispose() call race.
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            events.Writer.TryComplete();
            subscription.Dispose();
        }
    }
}
