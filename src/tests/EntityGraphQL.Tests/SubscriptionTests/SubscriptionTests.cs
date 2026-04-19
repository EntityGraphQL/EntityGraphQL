using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using EntityGraphQL.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
// Alias needed: TestDataContext.cs defines a non-generic 'Task' model class in the global namespace
// which shadows System.Threading.Tasks.Task for static member calls like Task.FromResult().
using SysTask = System.Threading.Tasks.Task;

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
        Assert.Contains("type Subscription {\n\tonMessage: Message!\n}", res);
    }

    [Fact]
    public void TestSubscriptionNotInSchemaOutput()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info");
        // add NO subscriptions
        var res = schema.ToGraphQLSchemaString();
        Assert.DoesNotContain("subscription: Subscription", res);
        Assert.DoesNotContain(
            @"type Subscription {
	onMessage: Message
}",
            res
        );
    }

    [Fact]
    public void TestSubscriptionInIntrospection()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info");
        schema.Subscription().AddFrom<TestSubscriptions>();
        var gql = new QueryRequest
        {
            Query =
                @"query {
                  sub: __type(name: ""Subscription"") {
                    name fields { name }
                  }
                }",
        };
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(res.Errors);
        dynamic data = res.Data!["sub"]!;
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
            Query =
                @"subscription {
                  onMessage { id text }
                }",
        };
        var res = GraphQLParser.Parse(gql, schema);
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
        schema
            .Subscription()
            .Add(
                "secondOne",
                (ChatService chat) =>
                {
                    return chat.Subscribe();
                }
            );
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"subscription {
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
        schema
            .Subscription()
            .Add(
                "onMessage",
                (ChatService chat, string user) =>
                {
                    return chat.Subscribe();
                }
            );
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"subscription {
                  onMessage(user: ""Joe"") { id text }
                }",
        };
        var services = new ServiceCollection();
        services.AddSingleton(new ChatService());
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), services.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
    }

    [Fact]
    public void TestSubscriptionReturnsListResult()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        schema
            .Subscription()
            .Add(
                "onMessages",
                (ChatService chat) =>
                {
                    return chat.SubscribeToList();
                }
            );
        var gql = new QueryRequest
        {
            Query =
                @"subscription {
                  onMessages { id text }
                }",
        };
        var services = new ServiceCollection();
        services.AddSingleton(new ChatService());
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), services.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
    }

    [Fact]
    public void TestTaskObservableSubscriptionRegisters()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        // Task<IObservable<T>> should be accepted during registration
        schema.Subscription().AddFrom<AsyncTestSubscriptions>();
        var res = schema.ToGraphQLSchemaString();
        Assert.Contains("onMessageAsync: Message!", res);
    }

    [Fact]
    public void TestValueTaskObservableSubscriptionRegisters()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        // ValueTask<IObservable<T>> should be accepted during registration
        schema.Subscription().Add("onMessageValueTask", (ChatService chat) => new ValueTask<IObservable<Message>>(chat.Subscribe()));
        var res = schema.ToGraphQLSchemaString();
        Assert.Contains("onMessageValueTask: Message!", res);
    }

    [Fact]
    public void TestInvalidAsyncSubscriptionThrows()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        // Task<string> is not a valid subscription return type
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Subscription().AddFrom<BadAsyncSubscriptions>());
        Assert.Contains("IObservable", ex.Message);
    }

    [Fact]
    public void TestTaskObservableSubscriptionExecutes()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        schema.Subscription().AddFrom<AsyncTestSubscriptions>();
        var gql = new QueryRequest
        {
            Query =
                @"subscription {
                  onMessageAsync { id text }
                }",
        };
        var services = new ServiceCollection();
        var chat = new ChatService();
        services.AddSingleton(chat);
        services.AddSingleton(new TestDataContext());
        var sp = services.BuildServiceProvider();

        // Setup: subscription method is called, observable is returned
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), sp, null);
        Assert.Null(res.Errors);
        var subscribeResult = res.Data?.Values.First() as GraphQLSubscribeResult;
        Assert.NotNull(subscribeResult);
        Assert.Equal(typeof(Message), subscribeResult.EventType);

        // Event: fire a message and verify the GraphQL projection runs correctly
        var msg = chat.PostMessage("hello");
        dynamic? data = subscribeResult.SubscriptionStatement.ExecuteSubscriptionEvent<TestDataContext, Message>(
            subscribeResult.Field,
            msg,
            sp,
            new QueryRequestContext(schema.AuthorizationService, null)
        );
        Assert.NotNull(data);
        Assert.Equal(msg.Id, (int)data!.id);
        Assert.Equal("hello", (string)data!.text);
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

internal class AsyncTestSubscriptions
{
    [GraphQLSubscription]
    public Task<IObservable<Message>> OnMessageAsync(ChatService chat)
    {
        return SysTask.FromResult(chat.Subscribe());
    }
}

internal class BadAsyncSubscriptions
{
    [GraphQLSubscription]
    public Task<string> BadSubscription()
    {
        return SysTask.FromResult("not an observable");
    }
}

internal class Message
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
}

internal class ChatService
{
    private readonly Broadcaster<Message> broadcaster = new();
    private readonly Broadcaster<List<Message>> listBroadcaster = new();

    public Message PostMessage(string message)
    {
        var msg = new Message { Id = 2, Text = message };

        broadcaster.OnNext(msg);

        return msg;
    }

    public IObservable<Message> Subscribe()
    {
        return broadcaster;
    }

    public IObservable<List<Message>> SubscribeToList()
    {
        return listBroadcaster;
    }
}
