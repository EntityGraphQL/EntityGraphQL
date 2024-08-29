using System;
using System.Collections.Generic;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet.WebSockets;

/// <summary>
/// Ties the GraphQL subscription to the WebSocket connection.
/// </summary>
/// <typeparam name="TEventType"></typeparam>
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
            server.SendNextAsync(OperationId, result).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
    }

    public void OnError(Exception error)
    {
        server.SendErrorAsync(OperationId, error).GetAwaiter().GetResult();
    }

    public void OnCompleted()
    {
        server.CompleteSubscriptionAsync(OperationId);
    }

    public void Dispose()
    {
        subscription.Dispose();
    }
}
