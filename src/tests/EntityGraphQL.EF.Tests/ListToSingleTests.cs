using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class ListToSingleTests
{
    [Fact]
    public void TestListToSingle()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();

        schema.UpdateType<Movie>(schemaType =>
        {
            schemaType.AddField("mainActor", movie => movie.Actors.FirstOrDefault(a => a.Id == 1), null);
        });

        var gql = new QueryRequest
        {
            Query =
                @"{
                    movies {
                        mainActor { name }
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ActorService>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        serviceCollection.AddSingleton(data);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        data.Movies.AddRange(new Movie("A New Hope") { Id = 10, Actors = [new("Alec Guinness") { Id = 1 }, new("Mark Hamill") { Id = 2 }] });
        data.SaveChanges();
        var result = schema.ExecuteRequest(gql, serviceProvider, null);
        Assert.Null(result.Errors);

        dynamic movies = result.Data!["movies"]!;
        Assert.Equal(1, movies.Count);
        Assert.Equal("Alec Guinness", movies[0].mainActor.name);
    }

    [Fact]
    public void TestListToSingleWithFind()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();

        schema.UpdateQuery(schemaType =>
        {
            schemaType.ReplaceField("actor", new { id = ArgumentHelper.Required<int>() }, (ctx, args) => ctx.Actors.Find(args.id.Value), null);
        });

        var gql = new QueryRequest
        {
            Query =
                @"{
                    actor(id: 1) {
                        name 
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        serviceCollection.AddSingleton(data);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        data.Actors.AddRange(
            new List<Actor>
            {
                new("Alec Guinness") { Id = 1 },
                new("Mark Hamill") { Id = 2 }
            }
        );
        data.SaveChanges();
        var result = schema.ExecuteRequest(gql, serviceProvider, null);
        Assert.Null(result.Errors);

        dynamic actor = result.Data!["actor"]!;
        Assert.Equal("Alec Guinness", actor.name);
    }
}
