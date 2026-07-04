using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class FilterExtensionTests
{
    [Fact]
    public void SupportNullableDateTime()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("actors", db => db.Actors, "Get all actors").UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    actors(filter: $filter) { id name }
                }",
            Variables = new QueryVariables { { "filter", "birthday > \"2024-09-08T07:00:00.000Z\"" } },
        };
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        var actor1 = new Actor("Actor1") { Id = 33, Birthday = new DateTime(2024, 9, 9) };
        data.Add(actor1);
        var actor2 = new Actor("Actor2") { Id = 98, Birthday = new DateTime(2024, 9, 7) };
        data.Add(actor2);
        data.SaveChanges();
        Assert.Equal(2, data.Actors.Count());
        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["actors"]!;
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal(33, person.id);
    }

    [Fact]
    public void GitHubIssue378_FilterOnPagedChildFieldTranslatesToSql()
    {
        // filtering by a paged child field ("movies.items.count() > 0") - the filter resolves the paged
        // field to its underlying collection so it executes against EF (sqlite throws if untranslatable)
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Type<Actor>().GetField("movies", null).UseOffsetPaging();
        schema.Query().ReplaceField("actors", db => db.Actors, "Get all actors").UseFilter();

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        var movie = new Movie("A movie");
        data.Add(new Actor("With movies") { Id = 1, Movies = [movie] });
        data.Add(new Actor("Without movies") { Id = 2 });
        data.SaveChanges();

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = @"{ actors(filter: ""movies.items.count() > 0"") { id name } }" },
            data,
            null,
            null
        );

        Assert.Null(result.Errors);
        dynamic actors = ((IDictionary<string, object>)result.Data!)["actors"]!;
        Assert.Equal(1, Enumerable.Count(actors));
        Assert.Equal(1, Enumerable.First(actors).id);
    }
}
