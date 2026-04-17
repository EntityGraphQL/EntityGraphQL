using System;
using System.Collections.Generic;
using System.Threading;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet.WebSockets;

/// <summary>
/// Ties the GraphQL subscription to the WebSocket connection.
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

    public WebSocketSubscription(string id, object observable, IGraphQLWebSocketServer server, GraphQLSubscriptionStatement subscriptionStatement, GraphQLSubscriptionField node)
    {
        this.OperationId = id;
        if (observable is not IObservable<TEventType>)
            throw new ArgumentException($"{nameof(observable)} must be of type {nameof(IObservable<TEventType>)}");
        this.observable = (IObservable<TEventType>)observable;
        this.server = server;
        this.subscriptionStatement = subscriptionStatement;
        this.subscriptionNode = node;

        this.subscription = this.observable.Subscribe(this);
    }

    public void OnNext(TEventType value)
    {
        try
        {
            var data = subscriptionStatement.ExecuteSubscriptionEvent<TQueryContext, TEventType>(
                subscriptionNode,
                value,
                server.Context.RequestServices,
                new QueryRequestContext(subscriptionStatement.Schema.AuthorizationService, server.Context.User)
            );
            var result = new QueryResult();
            result.SetData(new Dictionary<string, object?> { { subscriptionNode.Name, data } });
            // SendNextAsync only enqueues — never blocks, never throws.
            server.SendNextAsync(OperationId, result);
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
    }

    public void OnError(Exception error)
    {
        // SendErrorAsync only enqueues — the drain task delivers it without blocking this thread.
        server.SendErrorAsync(OperationId, error);
        // Per the Rx contract, OnError means the sequence has terminated.
        // Remove the subscription from the server so no further events are processed.
        server.CompleteSubscriptionAsync(OperationId);
    }

    public void OnCompleted()
    {
        // The observable sequence has ended normally; clean up the server-side subscription.
        server.CompleteSubscriptionAsync(OperationId);
    }

    public void Dispose()
    {
        // Interlocked ensures the inner subscription is disposed exactly once even if
        // CompleteSubscriptionAsync and an explicit Dispose() call race.
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            subscription.Dispose();
    }
}
