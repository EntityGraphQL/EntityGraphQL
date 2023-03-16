using System;
using System.Collections.Generic;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.AspNet.WebSockets
{
    /// <summary>
    /// Ties the GraphQL subscription to the WebSocket connection.
    /// </summary>
    /// <typeparam name="TEventType"></typeparam>
    internal sealed class WebSocketSubscription<TQueryContext, TEventType> : IDisposable, IObserver<TEventType>
    {
        private readonly Guid id;
        private readonly IObservable<TEventType> observable;
        private readonly IGraphQLWebSocketServer server;
        private readonly IDisposable subscription;
        private readonly GraphQLSubscriptionStatement subscriptionStatement;
        private readonly GraphQLSubscriptionField subscriptionNode;

        public WebSocketSubscription(Guid id, object observable, IGraphQLWebSocketServer server, GraphQLSubscriptionStatement subscriptionStatement, GraphQLSubscriptionField node)
        {
            this.id = id;
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
                var data = subscriptionStatement.ExecuteSubscriptionEvent<TQueryContext, TEventType>(subscriptionNode, value);
                var result = new QueryResult();
                result.SetData(new Dictionary<string, object?> { { subscriptionNode.Name, data } });
                server.SendNextAsync(id, result).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        public void OnError(Exception error)
        {
            server.SendErrorAsync(id, error).GetAwaiter().GetResult();
        }

        public void OnCompleted()
        {
            server.CompleteSubscription(id);
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}