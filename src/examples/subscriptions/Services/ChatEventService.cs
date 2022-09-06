using System.Linq.Expressions;
using EntityGraphQL.Subscriptions;

namespace subscriptions.Services;

public class ChatEventService
{
    private readonly Broadcaster<Expression<Func<ChatContext, MessageEvent>>> broadcaster = new();

    public Message PostEvent(ChatContext db, MessageEventType eventType, string message, string user)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Text = message,
            Timestamp = DateTime.Now,
            UserId = user,
        };

        // you could store the message where ever you need i.e. the DB via ChatContext
        db.Messages.Add(msg);
        // hack to make sure the user is there
        if (!db.Users.Any(u => u.Id == user))
        {
            db.Users.Add(new User { Id = user, Name = $"{user} Kim" });
        }
        db.SaveChanges();

        // return an expression that uses the ChatContext
        try
        {
            broadcaster.OnNext(ctx => new MessageEvent { Message = ctx.Messages.First(m => m.Id == msg.Id), EventType = eventType });
        }
        catch (Exception e)
        {
            broadcaster.OnError(e);
        }

        return msg;
    }

    public IObservable<Expression<Func<ChatContext, MessageEvent>>> Subscribe()
    {
        return broadcaster;
    }
}

public enum MessageEventType
{
    New,
    Edit,
    Delete
}

public class MessageEvent
{
#nullable disable
    public Message Message { get; set; }
#nullable enable
    public MessageEventType EventType { get; set; }
}
