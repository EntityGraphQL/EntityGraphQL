using EntityGraphQL.Schema;
using EntityGraphQL.Subscriptions;

namespace EntityGraphQL.AspNet.Tests
{
    internal class TestQueryContext
    {
    }

    internal class TestSubscription
    {
        [GraphQLSubscription("Example of a subscription")]
        public static IObservable<Message> OnMessage(TestChatService chat)
        {
            return chat.Subscribe();
        }
    }

    internal class TestChatService
    {
        private readonly List<Message> messages = new();
        private readonly Broadcaster<Message> broadcaster = new();

        public Message PostMessage(string message)
        {
            var msg = new Message
            {
                Id = Guid.NewGuid(),
                Text = message,
                Timestamp = DateTime.Now,
            };

            lock (messages)
                messages.Add(msg);

            broadcaster.OnNext(msg);

            return msg;
        }

        public IObservable<Message> Subscribe()
        {
            return broadcaster;
        }
    }

    internal class Message
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public DateTime Timestamp { get; set; }
    }
}