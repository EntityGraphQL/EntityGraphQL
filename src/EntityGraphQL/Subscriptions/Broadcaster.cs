using System;
using System.Collections.Generic;

namespace EntityGraphQL.Subscriptions
{
    /// <summary>
    /// Simple class to broadcast messages to all subscribers.
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
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public class Broadcaster<TType> : IObservable<TType>
    {
        private readonly List<IObserver<TType>> observers = new();
        public IDisposable Subscribe(IObserver<TType> observer)
        {
            observers.Add(observer);
            return new GraphQLSubscription<TType>(this, observer);
        }

        public void Unsubscribe(IObserver<TType> observer)
        {
            observers.Remove(observer);
        }

        public void OnNext(TType value)
        {
            foreach (var observer in observers)
            {
                observer.OnNext(value);
            }
        }
    }
}