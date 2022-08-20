using System.Linq.Expressions;
using EntityGraphQL.Schema;
using subscriptions.Services;

namespace subscriptions.Mutations
{
    public class ChatMutations
    {
        [GraphQLMutation]
        public Expression<Func<ChatContext, Message>> PostMessage(string message, string user, ChatService chat)
        {
            var postedMessage = chat.PostMessage(message, user);
            // using the expression allows us to join back to the main schema if we want
            return _ => postedMessage;
        }
    }
}