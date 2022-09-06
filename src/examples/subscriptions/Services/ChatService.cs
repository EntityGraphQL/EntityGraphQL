using EntityGraphQL.Subscriptions;

namespace subscriptions.Services;

public class ChatService
{
    private readonly Broadcaster<Message> broadcaster = new();

    public Message PostMessage(ChatContext db, string message, string user)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Text = message,
            Timestamp = DateTime.Now,
            UserId = user,
        };

        db.Messages.Add(msg);
        db.SaveChanges();

        broadcaster.OnNext(msg);

        return msg;
    }

    public IObservable<Message> Subscribe()
    {
        return broadcaster;
    }
}