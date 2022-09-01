using EntityGraphQL.Schema;
using subscriptions.Mutations;
using subscriptions.Services;
using subscriptions.Subscriptions;

internal class ChatGraphQLSchema
{
    internal static void ConfigureSchema(SchemaProvider<ChatContext> schema)
    {
        schema.AddType<MessageEvent>("Message event info").AddAllFields();
        schema.Mutation().AddFrom<ChatMutations>();
        schema.Subscription().AddFrom<ChatSubscriptions>();
    }
}