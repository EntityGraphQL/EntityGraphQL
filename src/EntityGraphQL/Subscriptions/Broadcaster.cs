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
    public class Broadcaster<TType> : IObservable<TType>
    {
        private readonly List<IObserver<TType>> subscribers = new();

        /// <summary>
        /// Register an observer to the broadcaster.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <returns></returns>
        public IDisposable Subscribe(IObserver<TType> subscriber)
        {
            subscribers.Add(subscriber);
            return new GraphQLSubscription<TType>(this, subscriber);
        }

        public void Unsubscribe(IObserver<TType> observer)
        {
            subscribers.Remove(observer);
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
    }
}