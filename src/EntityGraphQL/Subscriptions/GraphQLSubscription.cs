using System;

namespace EntityGraphQL.Subscriptions
{
    /// <summary>
    /// Represents a GraphQL subscription.
    /// </summary>
    public interface IGraphQLSubscription : IDisposable
    {
        Action? OnCompleted { get; set; }
        Action<Exception>? OnError { get; set; }
        Action<QueryResult>? OnNext { get; set; }
    }

    /// <summary>
    /// Used as the return type of Broadcaster.Subscribe. 
    /// Holds the broadcaster and the observer to correctly unsubscribe if this subscription is disposed of.
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public class GraphQLSubscription<TType> : IGraphQLSubscription
    {
        private readonly Broadcaster<TType> broadcaster;
        private readonly IObserver<TType> observer;

        public GraphQLSubscription(Broadcaster<TType> broadcaster, IObserver<TType> observer)
        {
            this.broadcaster = broadcaster;
            this.observer = observer;
        }

        public Action? OnCompleted { get; set; }

        public Action<Exception>? OnError { get; set; }

        public Action<QueryResult>? OnNext { get; set; }

        public void Dispose()
        {
            broadcaster.Unsubscribe(observer);
            GC.SuppressFinalize(this);
        }
    }
}