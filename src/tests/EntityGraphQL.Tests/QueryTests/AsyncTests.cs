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
        // Expression have no concept of async/await as it is a compiler feature so you need to use
        // .GetAwaiter().GetResult() on your async methods
        schema.Type<Person>().AddField("age", "Returns persons age")
            .ResolveWithService<AgeService>((ctx, srv) => srv.GetAgeAsync(ctx.Birthday).GetAwaiter().GetResult());

        var gql = new QueryRequest
        {
            Query = @"query {
                    people {
                        age
                    }
                }"
        };

        var context = new TestDataContext();
        context.People.Clear();
        context.People.Add(new Person
        {
            Birthday = DateTime.Now.AddYears(-2)
        });

        var serviceCollection = new ServiceCollection();
        AgeService service = new();
        serviceCollection.AddSingleton(service);

        var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);

        Assert.NotNull(res.Data);
        Assert.Equal(2, ((dynamic)res.Data["people"])[0].age);
    }

    [Fact]
    public void TestNonResolvedAsyncServiceFieldErrors()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // Error as we return a Task<>
        Assert.Throws<EntityGraphQLCompilerException>(() => schema.Type<Person>().AddField("age", "Returns persons age")
            .ResolveWithService<AgeService>((ctx, srv) => srv.GetAgeAsync(ctx.Birthday)));
    }

    [Fact]
    public void TestReturnsTaskButNotAsync()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add(TestAddPersonAsync);
        Assert.Equal("Person", schema.Mutation().SchemaType.GetField("testAddPersonAsync", null).ReturnType.SchemaType.Name);
        Assert.Equal(typeof(Person), schema.Mutation().SchemaType.GetField("testAddPersonAsync", null).ReturnType.TypeDotnet);
    }

    private System.Threading.Tasks.Task<Person> TestAddPersonAsync()
    {
        return System.Threading.Tasks.Task.FromResult(new Person());
    }
}