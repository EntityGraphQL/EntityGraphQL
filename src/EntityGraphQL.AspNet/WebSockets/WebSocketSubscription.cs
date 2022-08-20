using System;
using System.Collections.Generic;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.AspNet.WebSockets
{
    /// <summary>
    /// Ties the GraphQL subscription to the WebSocket connection.
    /// 
    /// As GraphQL says nothing about the protocol or the delivery of the stream.
    /// </summary>
    /// <typeparam name="TEventType"></typeparam>
    internal class WebSocketSubscription<TEventType> : IWebSocketSubscription, IObserver<TEventType>
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
                var data = subscriptionStatement.ExecuteSubscriptionEvent(subscriptionNode, value);
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

    public interface IWebSocketSubscription : IDisposable
    {
    }
}