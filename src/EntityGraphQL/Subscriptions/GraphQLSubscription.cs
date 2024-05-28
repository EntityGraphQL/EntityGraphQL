using System;

namespace EntityGraphQL.Subscriptions;

/// <summary>
/// Used as the return type of Broadcaster.Subscribe.
/// Holds the broadcaster and the observer to correctly unsubscribe if this subscription is disposed of.
/// </summary>
/// <typeparam name="TType"></typeparam>
public class GraphQLSubscription<TType>(Broadcaster<TType> broadcaster, IObserver<TType> observer) : IDisposable
{
    private readonly Broadcaster<TType> broadcaster = broadcaster;
    private readonly IObserver<TType> observer = observer;

    public void Dispose()
    {
        broadcaster.Unsubscribe(observer);
        GC.SuppressFinalize(this);
    }
}
