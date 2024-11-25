using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class FieldExtensionsOnQueryableTests
{
    [Fact]
    public void TestServicesInOffsetPaging()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.UpdateQuery(queryType =>
        {
            queryType.AddField("moviesByOffset", ctx => ctx.Movies, null).UseOffsetPaging();
        });

        schema.UpdateType<Movie>(schemaType =>
        {
            schemaType.ReplaceField("actors", null).Resolve<ActorService>((m, actors) => ActorService.GetByMovie(m.Id));
            schemaType.AddField("mainActor", null).Resolve<ActorService>((m, actors) => ActorService.GetFirstActorNameByMovie(m.Id));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{
                    moviesByOffset {
                        items {
                            mainActor
                            actors { name }
                        }
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ActorService>();

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        serviceCollection.AddSingleton(data);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        data.Movies.AddRange(new Movie("A New Hope") { Id = 1 }, new Movie("The Empire Strike Back") { Id = 2 }, new Movie("Return of the Jedi") { Id = 3 });
        data.SaveChanges();
        var result = schema.ExecuteRequest(gql, serviceProvider, null);
        Assert.Null(result.Errors);

        dynamic movies = result.Data!["moviesByOffset"]!;
        Assert.Equal(3, movies.items.Count);
        Assert.Equal("Alec Guinness", movies.items[0].mainActor);
        Assert.Equal(2, movies.items[0].actors.Count);
        Assert.Equal("Alec Guinness", movies.items[0].actors[0].name);
        Assert.Equal("Mark Hamill", movies.items[0].actors[1].name);
    }
}

internal class ActorService
{
    private static readonly IDictionary<int, IEnumerable<Actor>> peopleByMovies = new Dictionary<int, IEnumerable<Actor>>()
    {
        [1] = [new("Alec Guinness") { Id = 1 }, new("Mark Hamill") { Id = 2 }],
        [2] = [new("Carrie Fisher") { Id = 1 }, new("Mark Hamill") { Id = 2 }],
        [3] = [new("Harrison Ford") { Id = 1 }, new("Mark Hamill") { Id = 2 }],
    };

    public static IEnumerable<Actor> GetByMovie(int movieId)
    {
        return peopleByMovies.ContainsKey(movieId) ? peopleByMovies[movieId] : Array.Empty<Actor>();
    }

    public static string? GetFirstActorNameByMovie(int movieId)
    {
        return peopleByMovies.ContainsKey(movieId) ? peopleByMovies[movieId].FirstOrDefault()?.Name : null;
    }
}
