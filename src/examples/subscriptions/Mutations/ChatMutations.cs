using System.Linq.Expressions;
using EntityGraphQL.Schema;
using subscriptions.Services;

namespace subscriptions.Mutations
{
    public class ChatMutations
    {
        [GraphQLMutation]
        public static Expression<Func<ChatContext, Message>> PostMessage(string message, string user, ChatContext db, ChatService chat)
        {
            var postedMessage = chat.PostMessage(db, message, user);
            // using the expression allows us to join back to the main schema if we want
            return ctx => ctx.Messages.First(message => message.Id == postedMessage.Id);
        }
        [GraphQLMutation]
        public static Expression<Func<ChatContext, Message>> PostMessageEvent(string message, string user, ChatContext db, ChatEventService chatEvents)
        {
            var postedMessage = chatEvents.PostEvent(db, MessageEventType.New, message, user);
            // using the expression allows us to join back to the main schema if we want
            return ctx => ctx.Messages.First(message => message.Id == postedMessage.Id);
        }
    }
}