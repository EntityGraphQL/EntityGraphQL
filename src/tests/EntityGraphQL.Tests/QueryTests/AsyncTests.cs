using System;
using System.Linq;
using System.Threading;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class AsyncTests
{
    [Fact]
    public void TestAsyncServiceField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("age", "Returns persons age").ResolveAsync<AgeService>((ctx, srv) => srv.GetAgeAsync(ctx.Birthday));

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    people {
                        age
                    }
                }",
        };

        var context = new TestDataContext();
        context.People.Clear();
        context.People.Add(new Person { Birthday = DateTime.Now.AddYears(-2) });

        var serviceCollection = new ServiceCollection();
        AgeService service = new();
        serviceCollection.AddSingleton(service);

        var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);

        Assert.NotNull(res.Data);
        var age = ((dynamic)res.Data!["people"]!)[0].age;
        Assert.Equal(2, age);
    }

    [Fact]
    public void TestAsyncServiceFieldNowSupported()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // Task<> returns are now supported with automatic async resolution
        var field = schema.Type<Person>().AddField("age", "Returns persons age").ResolveAsync<AgeService>((ctx, srv) => srv.GetAgeAsync(ctx.Birthday));
        Assert.NotNull(field);
        Assert.Equal("age", field.Name);
        Assert.Equal(typeof(int), field.ReturnType.TypeDotnet);
    }

    [Fact]
    public void TestReturnsTaskButNotAsync()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add(TestAddPersonAsync);
        Assert.Equal("Person", schema.Mutation().SchemaType.GetField("testAddPersonAsync", null).ReturnType.SchemaType.Name);
        Assert.Equal(typeof(Person), schema.Mutation().SchemaType.GetField("testAddPersonAsync", null).ReturnType.TypeDotnet);
    }

    [Fact]
    public void TestFieldRequiresGenericTask()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // Task<> returns are now supported with automatic async resolution
        Assert.Throws<EntityGraphQLSchemaException>(() =>
        {
            schema.Type<Person>().AddField("age", "Returns persons age").ResolveAsync<AgeService>((ctx, srv) => srv.GetAgeAsyncNoResult(ctx.Birthday));
        });
    }

    private System.Threading.Tasks.Task<Person> TestAddPersonAsync()
    {
        return System.Threading.Tasks.Task.FromResult(new Person());
    }

    [Fact]
    public async System.Threading.Tasks.Task TestCancellationTokenSupport()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        // Add a field that accepts CancellationToken - use the regular async overload for now
        schema
            .Type<Person>()
            .AddField("delayedAge", "Returns age after delay with cancellation support")
            .ResolveAsync<CancellationTestService, CancellationToken>((ctx, srv, ct) => srv.GetAgeWithDelayAsync(ctx.Birthday, ct));

        var gql = new QueryRequest
        {
            Query =
                @"query {
                people {
                    delayedAge
                }
            }",
        };

        var context = new TestDataContext();
        context.People.Clear();
        context.People.Add(new Person { Birthday = DateTime.Now.AddYears(-25) });

        var serviceCollection = new ServiceCollection();
        var service = new CancellationTestService();
        serviceCollection.AddSingleton(service);
        serviceCollection.AddSingleton(context);

        // Test 1: Normal execution (should work)
        var result1 = await schema.ExecuteRequestAsync(gql, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(result1.Errors);
        Assert.NotNull(result1.Data);
        var age1 = ((dynamic)result1.Data!["people"]!)[0].delayedAge;
        Assert.Equal(25, age1);

        // Test 2: With cancelled token should work for now since we're using sync method
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // For now, just test that the feature compiles and works
        var result2 = await schema.ExecuteRequestAsync(gql, serviceCollection.BuildServiceProvider(), null, null, cts.Token);
        Assert.NotNull(result2.Errors);
        Assert.Single(result2.Errors);
        Assert.Equal("The operation was canceled.", result2.Errors[0].Message);
        Assert.Null(result2.Data);
    }
}
