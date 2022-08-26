using subscriptions.Services;
using EntityGraphQL.Schema;

namespace subscriptions.Subscriptions
{
    public class ChatSubscriptions
    {
        [GraphQLSubscription("Example of a subscription")]
        public IObservable<Message> OnMessage(ChatService chat)
        {
            return chat.Subscribe();
        }
    }
}