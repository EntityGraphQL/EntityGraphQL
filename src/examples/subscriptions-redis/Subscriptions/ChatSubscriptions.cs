using EntityGraphQL.Schema;
using subscriptions_redis.Services;

namespace subscriptions_redis.Subscriptions;

public class ChatSubscriptions
{
    /// <summary>
    /// Returns an observable that emits a new <see cref="Message"/> whenever any
    /// server instance posts one. The <c>Task</c> return type is supported by
    /// EntityGraphQL so that subscription setup can be async — useful when
    /// connecting to an external broker (Redis, RabbitMQ, etc.) requires an
    /// awaitable step before the observable is ready to deliver events.
    /// </summary>
    [GraphQLSubscription("Subscribe to new chat messages from any server instance")]
    public Task<IObservable<Message>> OnMessage(ChatService chat)
    {
        return chat.Subscribe();
    }
}
