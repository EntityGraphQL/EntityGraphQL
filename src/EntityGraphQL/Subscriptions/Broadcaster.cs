using System;
using System.Collections.Generic;

namespace EntityGraphQL.Subscriptions
{
    /// <summary>
    /// A simple class to broadcast messages to all subscribers.
    /// 
    /// Usage:
    /// public class ChatService
    /// {
    ///     private readonly Broadcaster<Message> broadcaster = new();
    ///     
    ///     public void PostMessage(string message, string user)
    ///     {
    ///         // ... do your logic
    ///         // broadcast the new message/event to all subscribers
    ///         broadcaster.OnNext(msg);
    ///     }
    ///     // return the broadcaster as an IObservable<>
    ///     public IObservable<Message> Subscribe()
    ///     {
    ///         return broadcaster;
    ///     }
    /// }
    /// 
    /// Note if your events are triggered from multiple services/servers you may want to implement different a broadcaster to handle those
    /// events (likely from some queue or service bus) to then pass them to the websocket subscriptions. Or you could use this class and 
    /// have a service wrap it that is listening to the queue or service bus.
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public class Broadcaster<TType> : IObservable<TType>, IDisposable
    {
        private readonly List<IObserver<TType>> subscribers = new();

        public Action<IObserver<TType>>? OnUnsubscribe { get; set; }

        /// <summary>
        /// Register an observer to the broadcaster.
        /// </summary>
        /// <param name="observer"></param>
        /// <returns></returns>
        public IDisposable Subscribe(IObserver<TType> observer)
        {
            subscribers.Add(observer);
            return new GraphQLSubscription<TType>(this, observer);
        }

        public void Unsubscribe(IObserver<TType> observer)
        {
            subscribers.Remove(observer);
            OnUnsubscribe?.Invoke(observer);
        }
        /// <summary>
        /// Broadcast the message to all subscribers.
        /// </summary>
        /// <param name="value"></param>
        public void OnNext(TType value)
        {
            foreach (var observer in subscribers)
            {
                observer.OnNext(value);
            }
        }
        public void OnError(Exception ex)
        {
            foreach (var observer in subscribers)
            {
                observer.OnError(ex);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            foreach (var observer in subscribers)
            {
                observer.OnCompleted();
            }
        }
    }
}