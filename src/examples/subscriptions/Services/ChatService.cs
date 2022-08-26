using EntityGraphQL.Subscriptions;

namespace subscriptions.Services;

public class ChatService
{
    private readonly List<Message> messages = new();
    private readonly Broadcaster<Message> broadcaster = new();

    public Message PostMessage(string message, string user)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Text = message,
            Timestamp = DateTime.Now,
            User = user,
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

public class Message
{
    public Guid Id { get; set; }
    public string Text { get; set; }
    public DateTime Timestamp { get; set; }
    public string User { get; set; }
}