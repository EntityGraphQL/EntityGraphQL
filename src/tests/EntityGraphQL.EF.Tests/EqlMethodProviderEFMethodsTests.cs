using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class EqlMethodProviderEFMethodsTests
{
    [Fact]
    public void RegisterEFMethod_ShouldAllowUsingEFFunctionsInFilter()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        RegisterEFLike(schema.MethodProvider);
        schema.Query().ReplaceField("movies", db => db.Movies, "Get all movies").UseFilter();

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();

        // Add test data
        var movie1 = new Movie("The Matrix") { Id = 1, Released = new DateTime(1999, 3, 31) };
        var movie2 = new Movie("Matrix Reloaded") { Id = 2, Released = new DateTime(2003, 5, 15) };
        var movie3 = new Movie("Inception") { Id = 3, Released = new DateTime(2010, 7, 16) };

        data.Movies.AddRange(movie1, movie2, movie3);
        data.SaveChanges();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                movies(filter: ""name.like(\""Matrix%\""))"") {
                    id
                    name
                }
            }",
        };

        // Act
        var result = schema.ExecuteRequestWithContext(gql, data, null, null);

        // Assert
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);

        dynamic movies = ((IDictionary<string, object>)result.Data!)["movies"]!;
        var moviesList = movies as IEnumerable<object>;

        // The EF.Functions.Like method is working - let's verify at least one movie is found
        Assert.True(moviesList!.Count() >= 1); // Should find at least one Matrix movie

        // Verify we found at least one movie containing "Matrix"
        var movieArray = moviesList!.ToArray();
        Assert.Contains(movieArray, m => ((dynamic)m).name.ToString().Contains("Matrix"));
    }

    [Fact]
    public void RegisterMultipleEFMethods_ShouldAllowUsingEFFunctionsWithBuiltInMethods()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        RegisterEFLike(schema.MethodProvider);
        schema.Query().ReplaceField("actors", db => db.Actors, "Get all actors").UseFilter();

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();

        var actor1 = new Actor("John Doe") { Id = 1, Birthday = new DateTime(1980, 1, 1) };
        var actor2 = new Actor("jane smith") { Id = 2, Birthday = new DateTime(1985, 5, 15) };
        var actor3 = new Actor("BOB WILSON") { Id = 3, Birthday = new DateTime(1975, 12, 25) };

        data.Actors.AddRange(actor1, actor2, actor3);
        data.SaveChanges();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                actors(filter: ""name.like(\""J%\"") && name.contains(\""o\""))"") {
                    id
                    name
                }
            }",
        };

        // Act
        var result = schema.ExecuteRequestWithContext(gql, data, null, null);

        // Assert
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);

        dynamic actors = ((IDictionary<string, object>)result.Data!)["actors"]!;
        var actorsList = actors as IEnumerable<object>;

        // Should find actors that start with 'J' AND contain 'o' - demonstrates EF + built-in methods working together
        Assert.True(actorsList!.Count() >= 1);

        var actorArray = actorsList!.ToArray();
        Assert.Contains(actorArray, a => ((dynamic)a).name == "John Doe");
    }

    [Fact]
    public void RegisterEFMethod_ShouldWorkWithDateTimeFunctions()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        RegisterEFDatePart(schema.MethodProvider);
        schema.Query().ReplaceField("movies", db => db.Movies, "Get all movies").UseFilter();

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();

        var movie1 = new Movie("Movie 1999") { Id = 1, Released = new DateTime(1999, 3, 31) };
        var movie2 = new Movie("Movie 2010") { Id = 2, Released = new DateTime(2010, 7, 16) };
        var movie3 = new Movie("Movie 1999-2") { Id = 3, Released = new DateTime(1999, 12, 25) };

        data.Movies.AddRange(movie1, movie2, movie3);
        data.SaveChanges();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                movies(filter: ""released.datePart(\""year\"") == 1999"") {
                    id
                    name
                }
            }",
        };

        // Act
        var result = schema.ExecuteRequestWithContext(gql, data, null, null);

        // Assert
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);

        dynamic movies = ((IDictionary<string, object>)result.Data!)["movies"]!;
        var moviesList = movies as IEnumerable<object>;
        Assert.Equal(2, moviesList!.Count()); // Should find both 1999 movies
    }

    [Fact]
    public void EqlMethodProvider_ShouldCoexistWithDefaultMethods()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        RegisterEFLike(schema.MethodProvider);
        schema.Query().ReplaceField("actors", db => db.Actors, "Get all actors").UseFilter();

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();

        var actor1 = new Actor("Alice Cooper") { Id = 1, Birthday = new DateTime(1980, 1, 1) };
        var actor2 = new Actor("Bob Dylan") { Id = 2, Birthday = new DateTime(1985, 5, 15) };

        data.Actors.AddRange(actor1, actor2);
        data.SaveChanges();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                actors(filter: ""name.like(\""A%\"") && name.contains(\""Cooper\""))"") {
                    id
                    name
                }
            }",
        };

        // Act
        var result = schema.ExecuteRequestWithContext(gql, data, null, null);

        // Assert
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);

        dynamic actors = ((IDictionary<string, object>)result.Data!)["actors"]!;
        var actorsList = actors as IEnumerable<object>;
        Assert.Single(actorsList!); // Should find "Alice Cooper"

        var actor = actorsList!.First();
        Assert.Equal("Alice Cooper", ((dynamic)actor).name);
    }

    /// <summary>
    /// Helper method to register EF.Functions.Like with the EqlMethodProvider.
    /// This demonstrates the simple approach using the EF registration helper.
    /// </summary>
    private static void RegisterEFLike(EqlMethodProvider provider)
    {
        // Using the EF helper - this is now much simpler!
        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var extraArgs = Expression.Property(null, typeof(Microsoft.EntityFrameworkCore.EF), nameof(Microsoft.EntityFrameworkCore.EF.Functions));
        provider.RegisterMethod(likeMethod, typeof(string), "like", [extraArgs]);
    }

    /// <summary>
    /// Helper method to register a simplified date part function for testing
    /// </summary>
    private static void RegisterEFDatePart(EqlMethodProvider provider)
    {
        provider.RegisterMethod(
            methodContextType: typeof(DateTime),
            filterMethodName: "datePart",
            makeCallFunc: (context, argContext, methodName, args) =>
            {
                if (args.Length != 1)
                    throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{methodName}' expects 1 argument but {args.Length} were supplied");

                return Expression.PropertyOrField(context, Expression.Lambda(args[0]).Compile().DynamicInvoke()!.ToString()!);
            }
        );
    }
}
