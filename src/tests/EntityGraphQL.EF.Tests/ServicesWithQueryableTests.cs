using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class ServicesWithQueryableTests
{
    [Fact]
    public void TestServiceFieldWithQueryable()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();

        schema.AddType<ProjectConfig>("Config").AddAllFields();

        schema.Type<Movie>().AddField("config", "Get configs if they exists").Resolve<ConfigService>((p, srv) => srv.Get(p.Id)).IsNullable(false);
        schema.Type<Movie>().AddField("mainActor", p => p.Actors.FirstOrDefault(), "Actor");

        var gql = new QueryRequest
        {
            Query =
                @"{
                     movies {
                         config { type }
                         mainActor { name }
                     }
                 }"
        };

        var serviceCollection = new ServiceCollection();
        var srv = new ConfigService();
        serviceCollection.AddSingleton(srv);
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        serviceCollection.AddSingleton(data);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        data.Database.EnsureCreated();
        data.Movies.AddRange(
            new Movie("A New Hope")
            {
                Id = 10,
                Actors = new List<Actor>
                {
                    new("Alec Guinness") { Id = 1 },
                    new("Mark Hamill") { Id = 2 }
                }
            }
        );
        data.SaveChanges();

        var res = schema.ExecuteRequest(gql, serviceProvider, null);
        Assert.Null(res.Errors);
        var movies = (dynamic)res.Data!["movies"]!;
        Type movieType = Enumerable.First(movies).GetType();
        Assert.Equal(2, movieType.GetFields().Length);
        Assert.Equal("config", movieType.GetFields()[0].Name);
        // null check should not cause multiple calls
        Assert.Equal(1, srv.CallCount);
    }

    [Fact]
    public void TestGenericMethodToUpdateType()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();

        AddExternalIdentifierProperty<Movie>(schema);

        var gql = new QueryRequest
        {
            Query =
                @"{
                    movies {
                        id
                        externalIdentifiers {
                            id
                            entityName
                        }
                    }
                 }"
        };

        var serviceCollection = new ServiceCollection();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        serviceCollection.AddSingleton(data);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        data.Movies.AddRange(
            new Movie("A New Hope")
            {
                Id = 10,
                Actors = new List<Actor>
                {
                    new Actor("Alec Guinness") { Id = 1 },
                    new Actor("Mark Hamill") { Id = 2 }
                }
            }
        );
        data.SaveChanges();

        var res = schema.ExecuteRequest(gql, serviceProvider, null);
        Assert.Null(res.Errors);
        var movies = (dynamic)res.Data!["movies"]!;
    }

    [Fact]
    public void TestServiceBackToDbManyToOne()
    {
        // { serviceField { dbField { childField { field } } } }
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.AddType<ProjectConfig>("Config").AddAllFields();

        schema.UpdateType<ProjectConfig>(config =>
        {
            // connect back to the main query types
            config.AddField("movie", "Get movie").ResolveWithService<TestDbContext>((c, db) => db.Movies.Where(m => m.Id == c.Id).FirstOrDefault());
        });

        schema.Query().AddField("configs", "Get configs").ResolveWithService<ConfigService>((p, srv) => srv.GetList(3, 100)).IsNullable(false);
        var gql = new QueryRequest
        {
            Query =
                @"{
                    configs {
                        id
                        movie {
                            director { name }
                        }
                    }
                 }"
        };

        var serviceCollection = new ServiceCollection();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        serviceCollection.AddTransient((sp) => factory.CreateContext());
        serviceCollection.AddSingleton(new ConfigService());
        var serviceProvider = serviceCollection.BuildServiceProvider();
        data.Directors.AddRange(new Director("George Lucas") { Id = 31 }, new Director("George Lucas1") { Id = 32 }, new Director("George Lucas2") { Id = 33 });
        data.Movies.AddRange(new Movie("A New Hope") { Id = 100, DirectorId = 31 }, new Movie("A New Hope1") { Id = 101, DirectorId = 32 }, new Movie("A New Hope2") { Id = 102, DirectorId = 33 });
        data.SaveChanges();

        var res = schema.ExecuteRequest(gql, serviceProvider, null);
        Assert.Null(res.Errors);
        var configs = (dynamic)res.Data!["configs"]!;
        // If correctly loaded by EF these should not be null
        // select should be inserted before the .FirstOrDefault()
        Assert.NotNull(configs[0].movie.director);
        Assert.NotNull(configs[1].movie.director);
        Assert.NotNull(configs[2].movie.director);
    }

    private static void AddExternalIdentifierProperty<TEntityType>(SchemaProvider<TestDbContext> schema)
        where TEntityType : IEntityWithId
    {
        schema
            .Type<TEntityType>()
            .AddField("externalIdentifiers", new { externalIdName = default(string) }, "External IDs")
            .Resolve<TestDbContext>(
                (movie, args, context) =>
                    context
                        .ExternalIdentifiers.WhereWhen(ei => ei.ExternalIdName == args.externalIdName, !string.IsNullOrWhiteSpace(args.externalIdName))
                        .Where(ei => ei.EntityName == context.GetEntityTableName<TEntityType>() && ei.EntityId == movie.Id)
            );
    }

    [Fact(Skip = "Not implemented")]
    public void TestFilterWithServiceReferenceNotSelected()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Actors.Add(new Actor("Jill") { Id = 1, Birthday = DateTime.Now.AddYears(-22) });
        data.Actors.Add(new Actor("Cheryl") { Id = 2, Birthday = DateTime.Now.AddYears(-10) });
        data.SaveChanges();

        schema.Query().ReplaceField("actors", ctx => ctx.Actors, "Return list of people").UseFilter();
        schema.Type<Actor>().AddField("age", "Persons age").ResolveWithService<AgeService>((person, ager) => ager.GetAge(person.Birthday));
        var gql = new QueryRequest
        {
            Query =
                @"{
                    actors(filter: ""age > 21"") {
                        name id
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        var ager = new AgeService();
        serviceCollection.AddSingleton(ager);
        serviceCollection.AddTransient((sp) => factory.CreateContext());

        var result = schema.ExecuteRequest(gql, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);
        Assert.Equal(1, ager.CallCount);

        dynamic actors = result.Data["actors"]!;
        Assert.Equal(1, Enumerable.Count(actors));
        var actor1 = Enumerable.ElementAt(actors, 0);
        Assert.Equal("Frank", actor1.lastName);
        Assert.Equal("Jill", actor1.name);
    }
}

internal static class ExternalIdentiferExtensions
{
    internal static string GetEntityTableName<TEntity>(this TestDbContext context) => context.Model.FindEntityType(typeof(TEntity))!.Name;
}
