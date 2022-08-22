
using System;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Subscriptions
{
    public class GraphQLSubscribeResult
    {
        public Type EventType { get; }

        private readonly object observable;

        public GraphQLSubscriptionStatement SubscriptionStatement { get; }
        public GraphQLSubscriptionField Field { get; }

        public GraphQLSubscribeResult(Type eventType, object result, GraphQLSubscriptionStatement graphQLSubscriptionStatement, GraphQLSubscriptionField node)
        {
            EventType = eventType;
            observable = result;
            SubscriptionStatement = graphQLSubscriptionStatement;
            Field = node;
        }

        public object GetObservable() => observable;
    }
}
