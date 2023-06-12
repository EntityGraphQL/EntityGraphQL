using System;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using EntityGraphQL.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
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
    public void TestSubscriptionInIntrospection()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info");
        schema.Subscription().AddFrom<TestSubscriptions>();
        var gql = new QueryRequest
        {
            Query = @"query {
                  sub: __type(name: ""Subscription"") {
                    name fields { name }
                  }
                }",
        };
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(res.Errors);
        dynamic data = res.Data["sub"];
        Assert.Single(data.fields);
        Assert.Equal("Subscription", data.name);
        Assert.Equal("onMessage", data.fields[0].name);
    }

    [Fact]
    public void TestSubscriptionMakesOperation()
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
    [Fact]
    public void TestOnlySingleRootField()
    {
        // https://spec.graphql.org/October2021/#sec-Single-root-field
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        schema.Subscription().AddFrom<TestSubscriptions>();
        schema.Subscription().Add("secondOne", (ChatService chat) => { return chat.Subscribe(); });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query = @"subscription {
                  onMessage { id text }
                  secondOne { text }
                }",
        };
        var services = new ServiceCollection();
        services.AddSingleton(new ChatService());
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), services.BuildServiceProvider(), null);
        Assert.NotNull(res.Errors);
        Assert.Equal("Subscription operations may only have a single root field. Field 'secondOne' should be used in another operation.", res.Errors[0].Message);
    }
    [Fact]
    public void TestArguments()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        schema.Subscription().Add("onMessage", (ChatService chat, string user) => { return chat.Subscribe(); });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query = @"subscription {
                  onMessage(user: ""Joe"") { id text }
                }",
        };
        var services = new ServiceCollection();
        services.AddSingleton(new ChatService());
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), services.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
    }
}

internal class TestSubscriptions
{
    [GraphQLSubscription]
    public IObservable<Message> OnMessage(ChatService chat)
    {
        return chat.Subscribe();
    }
}

internal class Message
{
    public int Id { get; set; }
    public string Text { get; set; }
}

internal class ChatService
{
    private readonly Broadcaster<Message> broadcaster = new();

    public Message PostMessage(string message)
    {
        var msg = new Message
        {
            Id = 2,
            Text = message,
        };

        broadcaster.OnNext(msg);

        return msg;
    }

    public IObservable<Message> Subscribe()
    {
        return broadcaster;
    }
}