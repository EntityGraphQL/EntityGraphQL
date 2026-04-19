using System.Text.Json;
using StackExchange.Redis;

namespace subscriptions_redis.Redis;

/// <summary>
/// An <see cref="IObservable{T}"/> that is backed by a Redis Pub/Sub channel.
/// Each subscriber gets its own Redis channel handler. When the subscription is disposed
/// the Redis handler is unsubscribed so no further events are delivered.
/// </summary>
public sealed class RedisObservable<T> : IObservable<T>
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _channelName;

    public RedisObservable(IConnectionMultiplexer redis, string channelName)
    {
        _redis = redis;
        _channelName = channelName;
    }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        var sub = _redis.GetSubscriber();
        var channel = RedisChannel.Literal(_channelName);

        Action<RedisChannel, RedisValue> handler = (_, value) =>
        {
            try
            {
                var msg = JsonSerializer.Deserialize<T>(value.ToString());
                if (msg is not null)
                    observer.OnNext(msg);
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        };

        sub.Subscribe(channel, handler);

        return new Subscription(sub, channel, handler);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ISubscriber _sub;
        private readonly RedisChannel _channel;
        private readonly Action<RedisChannel, RedisValue> _handler;
        private int _disposed;

        public Subscription(ISubscriber sub, RedisChannel channel, Action<RedisChannel, RedisValue> handler)
        {
            _sub = sub;
            _channel = channel;
            _handler = handler;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _sub.Unsubscribe(_channel, _handler);
        }
    }
}
