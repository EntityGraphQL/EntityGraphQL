using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

// Pins the SQL characteristics of the paging extensions against EF:
// - a COUNT query runs only when the selection needs it (totalCount/totalItems/pageInfo/hasNextPage/last)
// - the page query selects only the requested columns (projection pushed into SQL), LIMIT/OFFSET parameterized
// - two-pass service fields don't execute the page query twice; their deps ride the same page query
public class PagingSqlTests
{
    private static (TestDbContextFactory factory, TestDbContext data, List<string> sql) MakeLoggedContext()
    {
        var sql = new List<string>();
        var factory = new TestDbContextFactory();
        var data = factory.CreateContext(b => (DbContextOptionsBuilder<TestDbContext>)b.LogTo(m => sql.Add(m), new[] { RelationalEventId.CommandExecuted }));
        for (var i = 1; i <= 10; i++)
            data.Movies.Add(new Movie($"Movie{i}") { Id = i });
        data.SaveChanges();
        sql.Clear(); // drop setup commands
        return (factory, data, sql);
    }

    private static List<string> Commands(List<string> sql) => sql.Where(m => m.Contains("Executed DbCommand")).ToList();

    [Fact]
    public void ConnectionPagingWithCountIsTwoQueriesAndPrunesColumns()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseConnectionPaging();

        var (factory, data, sql) = MakeLoggedContext();
        using var _ = factory;

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ movies(first: 2, after: \"MQ==\") { totalCount pageInfo { hasNextPage endCursor } edges { node { name } cursor } } }" },
            data,
            null,
            null
        );

        Assert.Null(result.Errors);
        var commands = Commands(sql);
        Assert.Equal(2, commands.Count); // COUNT + page
        Assert.Contains("COUNT(*)", commands[0]);
        var page = commands[1];
        Assert.Contains("\"m\".\"Name\"", page); // requested column selected
        Assert.DoesNotContain("\"Released\"", page); // unrequested columns pruned
        Assert.Contains("LIMIT @", page); // parameterized paging -> plan-cache friendly
        Assert.Contains("OFFSET @", page);
    }

    [Fact]
    public void ConnectionPagingWithoutCountIsSingleQuery()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseConnectionPaging();

        var (factory, data, sql) = MakeLoggedContext();
        using var _ = factory;

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies(first: 2) { edges { node { name } } } }" }, data, null, null);

        Assert.Null(result.Errors);
        var commands = Commands(sql);
        Assert.Single(commands); // no COUNT when totalCount/pageInfo not selected
        Assert.DoesNotContain("COUNT", commands[0]);
    }

    [Fact]
    public void OffsetPagingCountOnlyWhenNeeded()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseOffsetPaging();

        var (factory, data, sql) = MakeLoggedContext();
        using var _ = factory;

        var r1 = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies(take: 2, skip: 4) { items { name } totalItems hasNextPage } }" }, data, null, null);
        Assert.Null(r1.Errors);
        Assert.Equal(2, Commands(sql).Count); // COUNT + page

        sql.Clear();
        var r2 = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies(take: 2, skip: 4) { items { name } } }" }, data, null, null);
        Assert.Null(r2.Errors);
        var commands = Commands(sql);
        Assert.Single(commands); // items only -> no COUNT
        Assert.Contains("LIMIT @", commands[0]);
    }

    [Fact]
    public void OffsetPagingHasNextPageOnlyUsesExistsNotCount()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseOffsetPaging();

        var (factory, data, sql) = MakeLoggedContext();
        using var _ = factory;

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies(take: 2, skip: 4) { items { name } hasNextPage } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic movies = result.Data!["movies"]!;
        Assert.True((bool)movies.hasNextPage); // 10 movies, page ends at 6
        var commands = Commands(sql);
        Assert.Equal(2, commands.Count); // page + EXISTS - no COUNT
        Assert.DoesNotContain(commands, c => c.Contains("COUNT"));
        Assert.Contains(commands, c => c.Contains("EXISTS"));

        // boundary: last page -> hasNextPage false
        sql.Clear();
        var r2 = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies(take: 2, skip: 8) { hasNextPage } }" }, data, null, null);
        Assert.Null(r2.Errors);
        dynamic m2 = r2.Data!["movies"]!;
        Assert.False((bool)m2.hasNextPage);
        Assert.DoesNotContain(Commands(sql), c => c.Contains("COUNT"));
    }

    [Fact]
    public void ConnectionPagingForwardHasNextPageOnlyUsesExistsNotCount()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseConnectionPaging();

        var (factory, data, sql) = MakeLoggedContext();
        using var _ = factory;

        // the common infinite-scroll selection: pageInfo { hasNextPage hasPreviousPage } - no totalCount/endCursor
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ movies(first: 2, after: \"NA==\") { pageInfo { hasNextPage hasPreviousPage } edges { node { name } cursor } } }" },
            data,
            null,
            null
        );

        Assert.Null(result.Errors);
        dynamic movies = result.Data!["movies"]!;
        Assert.True((bool)movies.pageInfo.hasNextPage); // 10 movies, page = 5,6
        Assert.True((bool)movies.pageInfo.hasPreviousPage);
        var commands = Commands(sql);
        Assert.Equal(2, commands.Count); // page + EXISTS - no COUNT
        Assert.DoesNotContain(commands, c => c.Contains("COUNT"));
        Assert.Contains(commands, c => c.Contains("EXISTS"));

        // endCursor needs the total, so selecting it still runs the COUNT
        sql.Clear();
        var r2 = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies(first: 2) { pageInfo { hasNextPage endCursor } edges { node { name } } } }" }, data, null, null);
        Assert.Null(r2.Errors);
        Assert.Contains(Commands(sql), c => c.Contains("COUNT"));
    }

    private static (TestDbContextFactory factory, TestDbContext data, List<string> sql, SchemaProvider<TestDbContext> schema) MakeNestedOffsetPaging()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Type<Actor>().ReplaceField("movies", new { }, (a, _) => a.Movies.OrderBy(m => m.Id), "Movies").UseOffsetPaging();

        var sql = new List<string>();
        var factory = new TestDbContextFactory();
        var data = factory.CreateContext(b => (DbContextOptionsBuilder<TestDbContext>)b.LogTo(m => sql.Add(m), new[] { RelationalEventId.CommandExecuted }));
        data.Actors.Add(new Actor("Actor") { Id = 1, Movies = Enumerable.Range(1, 10).Select(i => new Movie($"Movie{i}") { Id = i }).ToList() });
        data.SaveChanges();
        sql.Clear();
        return (factory, data, sql, schema);
    }

    [Fact]
    public void NestedOffsetPagingHasNextPageIsTranslated()
    {
        var (factory, data, sql, schema) = MakeNestedOffsetPaging();
        using var _ = factory;

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ actors { movies(take: 2, skip: 4) { hasNextPage items { name } } } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic movies = ((dynamic)result.Data!["actors"]!)[0].movies;
        Assert.True((bool)movies.hasNextPage);
        Assert.Equal("Movie5", (string)movies.items[0].name);
        var page = Assert.Single(Commands(sql));
        Assert.Contains("EXISTS", page);
        // hasNextPage evaluated in memory would load every column of every movie of every actor
        Assert.DoesNotContain("\"Released\"", page);

        sql.Clear();
        var last = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ actors { movies(take: 2, skip: 8) { hasNextPage } } }" }, data, null, null);
        Assert.Null(last.Errors);
        Assert.False((bool)((dynamic)last.Data!["actors"]!)[0].movies.hasNextPage);

        // no take - the page is the rest of the collection
        var noTake = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ actors { movies(skip: 2) { hasNextPage } } }" }, data, null, null);
        Assert.Null(noTake.Errors);
        Assert.False((bool)((dynamic)noTake.Data!["actors"]!)[0].movies.hasNextPage);
    }

    [Fact]
    public void NestedOffsetPagingSkipWithoutTakeDoesNotOverflow()
    {
        var (factory, data, sql, schema) = MakeNestedOffsetPaging();
        using var _ = factory;

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ actors { movies(skip: 7) { items { name } } } }" }, data, null, null);

        Assert.Null(result.Errors);
        var items = ((IEnumerable<dynamic>)((dynamic)result.Data!["actors"]!)[0].movies.items).Select(i => (string)i.name).ToList();
        Assert.Equal(["Movie8", "Movie9", "Movie10"], items);
        // EF pages a nested collection with `row <= skip + take`. A take of int.MaxValue overflows an int on SQL Server and
        // Postgres for any skip > 0 (SQLite is 64-bit so would not show it)
        Assert.DoesNotContain("2147483647", Assert.Single(Commands(sql)));
    }

    [Fact]
    public void ConnectionPagingWithServiceFieldSharesThePageQuery()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.AddType<ProjectConfig>("Config", "Config").AddAllFields();
        schema.Type<Movie>().AddField("config", "Get config").Resolve<ConfigService>((m, srv) => srv.Get(m.Id)).IsNullable(false);
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseConnectionPaging();

        var (factory, data, sql) = MakeLoggedContext();
        using var _ = factory;

        var services = new ServiceCollection();
        services.AddSingleton(new ConfigService());

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ movies(first: 2) { totalCount edges { node { name config { type } } cursor } } }" },
            data,
            services.BuildServiceProvider(),
            null
        );

        Assert.Null(result.Errors);
        var commands = Commands(sql);
        Assert.Equal(2, commands.Count); // COUNT + ONE page query; service runs in memory in pass 2
        var page = commands[1];
        Assert.Contains("egql__m_Id", page); // service dependency folded into the same page query
        Assert.Contains("LIMIT @", page);
    }
}
