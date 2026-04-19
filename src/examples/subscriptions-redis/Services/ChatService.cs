using System.Text.Json;
using StackExchange.Redis;
using subscriptions_redis.Redis;

namespace subscriptions_redis.Services;

/// <summary>
/// Handles message persistence and fan-out via Redis Pub/Sub.
///
/// Publishing: when a mutation posts a message it is serialised to JSON and
/// published on the "chat:messages" Redis channel. Any server that has a
/// subscriber on that channel (i.e. any running instance of this app) will
/// receive the event and push it down its own WebSocket connections.
///
/// Subscribing: returns <see cref="Task{IObservable{Message}}"/> to demonstrate
/// EntityGraphQL's support for async subscription setup. Here we ping Redis
/// before handing back the observable so we fail fast if Redis is unreachable.
/// In more complex scenarios this might involve async channel initialisation or
/// establishing a dedicated subscriber connection.
/// </summary>
public class ChatService
{
    private const string ChannelName = "chat:messages";

    private readonly IConnectionMultiplexer _redis;

    public ChatService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    /// <summary>Posts a message to the database and publishes it to Redis.</summary>
    public async Task<Message> PostMessageAsync(ChatContext db, string message, string user)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Text = message,
            Timestamp = DateTime.UtcNow,
            UserId = user,
        };

        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var json = JsonSerializer.Serialize(msg);
        await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(ChannelName), json);

        return msg;
    }

    /// <summary>
    /// Returns an observable that emits <see cref="Message"/> events received from
    /// the shared Redis channel. The <c>Task</c> wrapper demonstrates the async
    /// subscription return type supported by EntityGraphQL: the method pings Redis
    /// to verify connectivity before yielding the observable.
    /// </summary>
    public async Task<IObservable<Message>> Subscribe()
    {
        // Verify Redis is reachable before accepting the subscription.
        await _redis.GetSubscriber().PingAsync();
        return new RedisObservable<Message>(_redis, ChannelName);
    }
}
