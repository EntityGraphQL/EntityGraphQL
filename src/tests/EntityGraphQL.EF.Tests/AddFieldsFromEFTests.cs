using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Microsoft.EntityFrameworkCore;
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

    private class MovieExpressionFields
    {
        // expression factory - invoked once at schema build time, the expression is registered like
        // AddField().Resolve() so it composes into the database-bound pass
        [GraphQLField("nameUpper", "Movie name uppercased")]
        public static Expression<Func<Movie, string>> NameUpper() => m => m.Name.ToUpper();

        // extra expression parameters are services - standard two-pass with deps extracted from the expression
        [GraphQLField("config", "Config via service")]
        public static Expression<Func<Movie, ConfigService, ProjectConfig>> Config() => (m, srv) => srv.Get(m.Id);
    }

    [Fact]
    public void ExpressionMethod_NoService_ExecutesInTheDatabase()
    {
        var sqlLog = new List<string>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext(b => (DbContextOptionsBuilder<TestDbContext>)b.LogTo(s => sqlLog.Add(s), [DbLoggerCategory.Database.Command.Name]));
        data.Movies.Add(new Movie("Alien") { Id = 20 });
        data.SaveChanges();

        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Type<Movie>().AddFieldsFrom<MovieExpressionFields>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(data);

        sqlLog.Clear();
        var res = schema.ExecuteRequest(new QueryRequest { Query = "{ movies { nameUpper } }" }, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        dynamic movies = res.Data!["movies"]!;
        Assert.Equal("ALIEN", movies[0].nameUpper);
        // the expression translated to SQL and only the needed column was selected
        var select = sqlLog.First(s => s.Contains("SELECT"));
        Assert.Contains("upper", select.ToLower());
        Assert.DoesNotContain("Released", select);
    }

    [Fact]
    public void ExpressionMethod_WithService_TwoPassWithExtractedDeps()
    {
        var sqlLog = new List<string>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext(b => (DbContextOptionsBuilder<TestDbContext>)b.LogTo(s => sqlLog.Add(s), [DbLoggerCategory.Database.Command.Name]));
        data.Movies.Add(new Movie("Alien") { Id = 21 });
        data.SaveChanges();

        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.AddType<ProjectConfig>("Config").AddAllFields();
        schema.Type<Movie>().AddFieldsFrom<MovieExpressionFields>();

        var srv = new ConfigService();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(srv);
        serviceCollection.AddSingleton(data);

        sqlLog.Clear();
        var res = schema.ExecuteRequest(new QueryRequest { Query = "{ movies { config { type } } }" }, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        dynamic movies = res.Data!["movies"]!;
        Assert.Equal("Something", movies[0].config.type);
        Assert.Equal(1, srv.CallCount);
        // only the expression's dependency (Id) was selected in the database pass
        var select = sqlLog.First(s => s.Contains("SELECT"));
        Assert.Contains("Id", select);
        Assert.DoesNotContain("Released", select);
    }
}
