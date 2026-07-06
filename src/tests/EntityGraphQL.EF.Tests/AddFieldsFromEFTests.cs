using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

/// <summary>
/// AddFieldsFrom methods returning IQueryable compose with EF - the method executes returning the deferred
/// EF query and the engine builds the selection on top of it via EF's provider, so filters and projections
/// translate to SQL. Includes a service field below the method-backed root to exercise the two-pass split
/// </summary>
public class AddFieldsFromEFTests
{
    private class MovieQueries
    {
        [GraphQLField("moviesNamedLike", "Movies with a name containing the filter")]
        public static IQueryable<Movie> MoviesNamedLike(TestDbContext db, string filter) => db.Movies.Where(m => m.Name.Contains(filter));
    }

    [Fact]
    public void MethodBackedRootField_ComposesIntoEFQuery_WithServiceFieldBelow()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.AddQueryFieldsFrom<MovieQueries>();
        schema.AddType<ProjectConfig>("Config").AddAllFields();
        schema.Type<Movie>().AddField("config", "Get config").Resolve<ConfigService>((m, srv) => srv.Get(m.Id)).IsNullable(false);

        var serviceCollection = new ServiceCollection();
        var srv = new ConfigService();
        serviceCollection.AddSingleton(srv);
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        serviceCollection.AddSingleton(data);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        data.Database.EnsureCreated();
        data.Movies.AddRange(new Movie("A New Hope") { Id = 10 }, new Movie("The Empire Strikes Back") { Id = 11 }, new Movie("Alien") { Id = 12 });
        data.SaveChanges();

        var gql = new QueryRequest
        {
            Query =
                @"{
                    moviesNamedLike(filter: ""Hope"") {
                        name
                        config { type }
                    }
                }",
        };

        var res = schema.ExecuteRequest(gql, serviceProvider, null);
        Assert.Null(res.Errors);
        dynamic movies = res.Data!["moviesNamedLike"]!;
        Assert.Single(movies);
        Assert.Equal("A New Hope", movies[0].name);
        Assert.Equal("Something", movies[0].config.type);
        // service called once for the single matching row - the filter ran in the database, not in memory
        Assert.Equal(1, srv.CallCount);
    }
}
