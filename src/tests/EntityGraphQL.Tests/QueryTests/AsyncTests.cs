using System;
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
        Assert.Throws<EntityGraphQLCompilerException>(() =>
        {
            schema.Type<Person>().AddField("age", "Returns persons age").ResolveAsync<AgeService>((ctx, srv) => srv.GetAgeAsyncNoResult(ctx.Birthday));
        });
    }

    private System.Threading.Tasks.Task<Person> TestAddPersonAsync()
    {
        return System.Threading.Tasks.Task.FromResult(new Person());
    }
}
