using System;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests.SubscriptionTests;

public class SubscriptionTests
{
    [Fact]
    public void TestSubscriptionInSchemaOutput()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info");
        schema.Subscription().AddFrom<TestSubscriptions>();
        var res = schema.ToGraphQLSchemaString();
        Assert.Contains("subscription: Subscription", res);
        Assert.Contains(@"type Subscription {
	onMessage: Message
}", res);
    }

    [Fact]
    public void TestSubscriptionNotInSchemaOutput()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info");
        // add NO subscriptions
        var res = schema.ToGraphQLSchemaString();
        Assert.DoesNotContain("subscription: Subscription", res);
        Assert.DoesNotContain(@"type Subscription {
	onMessage: Message
}", res);
    }

    [Fact]
    public void TestSubscriptionQueryMakeOperation()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        schema.Subscription().AddFrom<TestSubscriptions>();
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query = @"subscription {
                  onMessage { id text }
                }",
        };
        var res = new GraphQLCompiler(schema).Compile(gql.Query, null);
        Assert.Single(res.Operations);
        Assert.Single(res.Operations[0].QueryFields);
        Assert.Equal("onMessage", res.Operations[0].QueryFields[0].Name);
    }
}

internal class TestSubscriptions
{
    [GraphQLSubscription]
    public IObservable<Message> OnMessage(ChatService chat)
    {
        return chat;
    }
}

internal class Message
{
    public int Id { get; set; }
    public string Text { get; set; }
}

internal class ChatService : IObservable<Message>
{
    public IDisposable Subscribe(IObserver<Message> observer)
    {
        return null;
    }
}