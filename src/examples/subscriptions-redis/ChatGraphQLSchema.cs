using EntityGraphQL.Schema;
using subscriptions_redis.Mutations;
using subscriptions_redis.Subscriptions;

internal class ChatGraphQLSchema
{
    internal static void ConfigureSchema(SchemaProvider<ChatContext> schema)
    {
        schema.Mutation().AddFrom<ChatMutations>();
        schema.Subscription().AddFrom<ChatSubscriptions>();
    }
}
