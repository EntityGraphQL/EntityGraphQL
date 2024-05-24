using System;
using System.Collections.Generic;

namespace EntityGraphQL.Subscriptions;

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
    public List<IObserver<TType>> Subscribers { get; } = [];

    public Action<IObserver<TType>>? OnUnsubscribe { get; set; }

    /// <summary>
    /// Register an observer to the broadcaster.
    /// </summary>
    /// <param name="observer"></param>
    /// <returns></returns>
    public virtual IDisposable Subscribe(IObserver<TType> observer)
    {
        Subscribers.Add(observer);
        return new GraphQLSubscription<TType>(this, observer);
    }

    public virtual void Unsubscribe(IObserver<TType> observer)
    {
        Subscribers.Remove(observer);
        OnUnsubscribe?.Invoke(observer);
    }

    /// <summary>
    /// Broadcast the message to all subscribers.
    /// </summary>
    /// <param name="value"></param>
    public virtual void OnNext(TType value)
    {
        foreach (var observer in Subscribers)
        {
            observer.OnNext(value);
        }
    }

    public virtual void OnError(Exception ex)
    {
        foreach (var observer in Subscribers)
        {
            observer.OnError(ex);
        }
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var observer in Subscribers)
        {
            observer.OnCompleted();
        }
    }
}
