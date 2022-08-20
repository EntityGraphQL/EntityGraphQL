
using System;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Subscriptions
{
    public class GraphQLSubscribeResult
    {
        public Type EventType { get; }
        /// <summary>
        /// Will be the IObservable<TQueryType>
        /// </summary>
        public object SubscriptionObservable { get; }
        public GraphQLSubscriptionStatement SubscriptionStatement { get; }
        public GraphQLSubscriptionField Field { get; }

        public GraphQLSubscribeResult(Type eventType, object result, GraphQLSubscriptionStatement graphQLSubscriptionStatement, GraphQLSubscriptionField node)
        {
            EventType = eventType;
            SubscriptionObservable = result;
            SubscriptionStatement = graphQLSubscriptionStatement;
            Field = node;
        }
    }
}
