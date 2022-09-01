using System;
using System.Linq.Expressions;
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
        [GraphQLSubscription("Example of a subscription that allows querying relations back on the main context")]
        public IObservable<Expression<Func<ChatContext, MessageEvent>>> OnMessageEvent(ChatEventService chat)
        {
            return chat.Subscribe();
        }
    }
}