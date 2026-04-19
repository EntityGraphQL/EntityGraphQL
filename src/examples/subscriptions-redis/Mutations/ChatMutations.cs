using System.Linq.Expressions;
using EntityGraphQL.Schema;
using subscriptions_redis.Services;

namespace subscriptions_redis.Mutations;

public class ChatMutations
{
    [GraphQLMutation]
    public static async Task<Expression<Func<ChatContext, Message>>> PostMessage(string message, string user, ChatContext db, ChatService chat)
    {
        var posted = await chat.PostMessageAsync(db, message, user);
        // Return an expression so EntityGraphQL can re-query from the main context,
        // allowing the caller to select any fields including navigation properties.
        return ctx => ctx.Messages.First(m => m.Id == posted.Id);
    }
}
