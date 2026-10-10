using System;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class PagingTests
{
    [Fact]
    public void Test1ToManySelfReferenceConnection()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(data);
        data.Actors.Add(new Actor("Parent") { Id = 1 });
        data.SaveChanges();
        data.Actors.Add(
            new Actor("Child")
            {
                Id = 2,
                Children = new List<Actor> { data.Actors.First() },
            }
        );
        data.SaveChanges();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        schema.Query().ReplaceField("actors", ctx => ctx.Actors.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    actors {
                        totalCount
                        edges {
                            node {
                                name
                                children {
                                    name
                                }
                            }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic actors = result.Data!["actors"]!;
        Assert.Equal(data.Actors.Count(), Enumerable.Count(actors.edges));
        Assert.Equal(data.Actors.Count(), actors.totalCount);
        Assert.Empty(actors.edges[0].node.children);
        Assert.Equal("Parent", actors.edges[0].node.name);
        Assert.Equal(1, actors.edges[1].node.children.Count);
        Assert.Equal("Child", actors.edges[1].node.name);
        Assert.Equal("Parent", actors.edges[1].node.children[0].name);
    }

    /// <summary>
    /// Verifies two-pass execution for connection paging with a child service field against a real EF database.
    /// First pass: SQL query selects only DB columns (including Birthday, needed as service input).
    /// Second pass: AgeService runs in-memory on the paged result set.
    /// The service must NOT be called for the totalCount query — only for entities in the page.
    /// </summary>
    [Fact]
    public void TestConnectionPagingWithChildServiceField_TwoPass()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();

        data.Actors.AddRange(
            new Actor("Actor1") { Id = 1, Birthday = DateTime.Now.AddYears(-30) },
            new Actor("Actor2") { Id = 2, Birthday = DateTime.Now.AddYears(-40) },
            new Actor("Actor3") { Id = 3, Birthday = DateTime.Now.AddYears(-50) }
        );
        data.SaveChanges();

        // Paged field - no service in its own resolver, pure IQueryable
        schema.Query().ReplaceField("actors", ctx => ctx.Actors.OrderBy(a => a.Id), "Return paged actors").UseConnectionPaging(defaultPageSize: 2);

        // Child service field - uses Birthday which must be selected in first pass
        var ageService = new AgeService();
        schema.Type<Actor>().AddField("age", "Actor age").Resolve<AgeService>((a, srv) => srv.GetAge(a.Birthday));

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(ageService);
        serviceCollection.AddSingleton(data);

        var gql = new QueryRequest
        {
            Query =
                @"{
                    actors {
                        totalCount
                        edges {
                            node { id name age }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequest(gql, serviceCollection.BuildServiceProvider(), null, new ExecutionOptions { ExecuteServiceFieldsSeparately = true });

        Assert.Null(result.Errors);
        dynamic actors = result.Data!["actors"]!;
        // totalCount comes from the DB count, not the page
        Assert.Equal(3, actors.totalCount);
        // Only first page returned
        Assert.Equal(2, Enumerable.Count(actors.edges));
        // Service called once per entity in the page — NOT for totalCount
        Assert.Equal(2, ageService.CallCount);
    }

    /// <summary>
    /// Verifies two-pass execution for offset paging with a child service field against a real EF database.
    /// First pass: SQL query selects only DB columns (including Birthday, needed as service input).
    /// Second pass: AgeService runs in-memory on the paged result set.
    /// </summary>
    [Fact]
    public void TestOffsetPagingWithChildServiceField_TwoPass()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();

        data.Actors.AddRange(
            new Actor("Actor1") { Id = 1, Birthday = DateTime.Now.AddYears(-30) },
            new Actor("Actor2") { Id = 2, Birthday = DateTime.Now.AddYears(-40) },
            new Actor("Actor3") { Id = 3, Birthday = DateTime.Now.AddYears(-50) }
        );
        data.SaveChanges();

        schema.Query().ReplaceField("actors", ctx => ctx.Actors.OrderBy(a => a.Id), "Return paged actors").UseOffsetPaging(defaultPageSize: 2);

        var ageService = new AgeService();
        schema.Type<Actor>().AddField("age", "Actor age").Resolve<AgeService>((a, srv) => srv.GetAge(a.Birthday));

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(ageService);
        serviceCollection.AddSingleton(data);

        var gql = new QueryRequest
        {
            Query =
                @"{
                    actors {
                        totalItems
                        items { id name age }
                    }
                }",
        };

        var result = schema.ExecuteRequest(gql, serviceCollection.BuildServiceProvider(), null, new ExecutionOptions { ExecuteServiceFieldsSeparately = true });

        Assert.Null(result.Errors);
        dynamic actors = result.Data!["actors"]!;
        Assert.Equal(3, actors.totalItems);
        Assert.Equal(2, Enumerable.Count(actors.items));
        // Service called once per entity in the page — NOT for totalItems
        Assert.Equal(2, ageService.CallCount);
    }

    /// <summary>
    /// Verifies that when a paged field's resolver uses a service that takes a parent DB field as input,
    /// the correct DB value is passed to the service. Because the paging resolver itself uses a service,
    /// it falls back to single-pass execution — but the parent entity (including its DB fields) must still
    /// be correctly available for the service call.
    /// </summary>
    [Theory]
    [InlineData(true)]
    public void TestConnectionPagingWithServiceUsingParentDbField(bool executeServiceFieldsSeparately)
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();

        data.Directors.AddRange(new Director("Director A") { Id = 10 }, new Director("Director B") { Id = 20 });
        data.SaveChanges();

        // ConfigService.GetList(count, from) returns `count` configs with IDs starting at `from`.
        // By using dir.Id as `from`, we can assert the service received the correct DB field value.
        var configService = new ConfigService();
        schema.AddType<ProjectConfig>("Config").AddAllFields();
        schema
            .Type<Director>()
            .AddField("pagedConfigs", "Configs sourced from service using director's DB Id")
            .Resolve<ConfigService>((dir, svc) => svc.GetList(3, dir.Id))
            .UseConnectionPaging(defaultPageSize: 2);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(configService);
        serviceCollection.AddSingleton(data);

        var gql = new QueryRequest
        {
            Query =
                @"{
                    directors {
                        id
                        pagedConfigs {
                            totalCount
                            edges { node { id } }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequest(gql, serviceCollection.BuildServiceProvider(), null, new ExecutionOptions { ExecuteServiceFieldsSeparately = executeServiceFieldsSeparately });

        Assert.Null(result.Errors);
        dynamic directors = result.Data!["directors"]!;
        Assert.Equal(2, Enumerable.Count(directors));

        // Director A (Id=10): GetList(3, 10) → configs with Ids 10,11,12 — page of 2 returned
        dynamic dir1 = directors[0];
        Assert.Equal(10, (int)dir1.id);
        Assert.Equal(3, (int)dir1.pagedConfigs.totalCount);
        Assert.Equal(2, Enumerable.Count(dir1.pagedConfigs.edges));
        Assert.Equal(10, (int)dir1.pagedConfigs.edges[0].node.id); // first config Id == director.Id

        // Director B (Id=20): GetList(3, 20) → configs with Ids 20,21,22 — page of 2 returned
        dynamic dir2 = directors[1];
        Assert.Equal(20, (int)dir2.id);
        Assert.Equal(3, (int)dir2.pagedConfigs.totalCount);
        Assert.Equal(2, Enumerable.Count(dir2.pagedConfigs.edges));
        Assert.Equal(20, (int)dir2.pagedConfigs.edges[0].node.id); // first config Id == director.Id
    }

    [Fact]
    public void Test1ToManySelfReferenceOffset()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(data);
        data.Actors.Add(new Actor("Parent") { Id = 1 });
        data.SaveChanges();
        data.Actors.Add(
            new Actor("Child")
            {
                Id = 2,
                Children = new List<Actor> { data.Actors.First() },
            }
        );
        data.SaveChanges();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        schema.Query().ReplaceField("actors", ctx => ctx.Actors.OrderBy(p => p.Id), "Return list of people with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    actors {
                        totalItems
                        items {
                            name
                            children {
                                name
                            }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic actors = result.Data!["actors"]!;
        Assert.Equal(data.Actors.Count(), Enumerable.Count(actors.items));
        Assert.Equal(data.Actors.Count(), actors.totalItems);
        Assert.Empty(actors.items[0].children);
        Assert.Equal("Parent", actors.items[0].name);
        Assert.Equal(1, actors.items[1].children.Count);
        Assert.Equal("Child", actors.items[1].name);
        Assert.Equal("Parent", actors.items[1].children[0].name);
    }

    // A paged collection selected on a parent object is part of the parent's projection, so EF has to translate
    // the paging - "The LINQ expression 'p_Movie => new Dynamic_items...' could not be translated" before.
    [Fact]
    public void TestNestedOffsetPagingOnNavigation()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = CreateActorWithMovies(factory);

        schema.Type<Actor>().ReplaceField("movies", new { }, (a, _) => a.Movies.OrderByDescending(m => m.Released), "Movies, newest first").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    actor(id: 1) {
                        movies(take: 1) {
                            totalItems
                            hasNextPage
                            items { name }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic movies = ((dynamic)result.Data!["actor"]!).movies;
        Assert.Equal(2, movies.totalItems);
        Assert.True(movies.hasNextPage);
        Assert.Equal(1, Enumerable.Count(movies.items));
        Assert.Equal("Newer", movies.items[0].name);
    }

    [Fact]
    public void TestNestedOffsetPagingOnNavigationHasNextPageOnly()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = CreateActorWithMovies(factory);

        schema.Type<Actor>().ReplaceField("movies", new { }, (a, _) => a.Movies.OrderByDescending(m => m.Released), "Movies, newest first").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    actors {
                        movies(skip: 1, take: 1) {
                            hasNextPage
                            items { name }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic movies = ((dynamic)result.Data!["actors"]!)[0].movies;
        Assert.False(movies.hasNextPage);
        Assert.Equal(1, Enumerable.Count(movies.items));
        Assert.Equal("Older", movies.items[0].name);
    }

    // A connection paged collection selected on a parent object is part of the parent's projection, so EF has to translate
    // the paging and can not run ApplyCursors - "The LINQ expression 'edgeNode => ...' could not be translated" before
    [Fact]
    public void TestNestedConnectionPagingOnNavigation()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = CreateActorWithMovies(factory);

        schema.Type<Actor>().ReplaceField("movies", a => a.Movies.OrderByDescending(m => m.Released), "Movies, newest first").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    actor(id: 1) {
                        movies(first: 1) {
                            totalCount
                            pageInfo { hasNextPage hasPreviousPage startCursor endCursor }
                            edges { cursor c2: cursor node { name } }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic movies = ((dynamic)result.Data!["actor"]!).movies;
        Assert.Equal(2, movies.totalCount);
        Assert.True(movies.pageInfo.hasNextPage);
        Assert.False(movies.pageInfo.hasPreviousPage);
        Assert.Equal(ConnectionHelper.SerializeCursor(1), movies.pageInfo.startCursor);
        Assert.Equal(ConnectionHelper.SerializeCursor(1), movies.pageInfo.endCursor);
        Assert.Equal(1, Enumerable.Count(movies.edges));
        Assert.Equal("Newer", movies.edges[0].node.name);
        Assert.Equal(ConnectionHelper.SerializeCursor(1), movies.edges[0].cursor);
        Assert.Equal(ConnectionHelper.SerializeCursor(1), movies.edges[0].c2);
    }

    [Fact]
    public void TestNestedConnectionPagingOnNavigationAfter()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = CreateActorWithMovies(factory);

        schema.Type<Actor>().ReplaceField("movies", a => a.Movies.OrderByDescending(m => m.Released), "Movies, newest first").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query = $@"{{ actors {{ movies(first: 1, after: ""{ConnectionHelper.SerializeCursor(1)}"") {{ pageInfo {{ hasNextPage }} edges {{ cursor node {{ name }} }} }} }} }}",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic movies = ((dynamic)result.Data!["actors"]!)[0].movies;
        Assert.False(movies.pageInfo.hasNextPage);
        Assert.Equal(1, Enumerable.Count(movies.edges));
        Assert.Equal("Older", movies.edges[0].node.name);
        Assert.Equal(ConnectionHelper.SerializeCursor(2), movies.edges[0].cursor);
    }

    // last pages from the end of each parent's own collection, not a total shared across the request
    [Fact]
    public void TestNestedConnectionPagingOnNavigationLast()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = CreateActorWithMovies(factory);
        data.Actors.Add(
            new Actor("Actor2")
            {
                Id = 2,
                Movies =
                [
                    new Movie("A") { Id = 3, Released = new DateTime(2019, 1, 1) },
                    new Movie("B") { Id = 4, Released = new DateTime(2018, 1, 1) },
                    new Movie("C") { Id = 5, Released = new DateTime(2017, 1, 1) },
                ],
            }
        );
        data.SaveChanges();

        schema.Type<Actor>().ReplaceField("movies", a => a.Movies.OrderByDescending(m => m.Released), "Movies, newest first").UseConnectionPaging();
        var gql = new QueryRequest { Query = @"{ actors { name movies(last: 2) { totalCount pageInfo { hasPreviousPage startCursor endCursor } edges { cursor node { name } } } } }" };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic actors = result.Data!["actors"]!;
        dynamic first = Enumerable.Single((IEnumerable<dynamic>)actors, a => a.name == "Actor").movies;
        Assert.Equal(2, first.totalCount);
        Assert.False(first.pageInfo.hasPreviousPage);
        Assert.Equal(new[] { "Newer", "Older" }, ((IEnumerable<dynamic>)first.edges).Select(e => (string)e.node.name));
        Assert.Equal(new[] { ConnectionHelper.SerializeCursor(1), ConnectionHelper.SerializeCursor(2) }, ((IEnumerable<dynamic>)first.edges).Select(e => (string)e.cursor));

        dynamic second = Enumerable.Single((IEnumerable<dynamic>)actors, a => a.name == "Actor2").movies;
        Assert.Equal(3, second.totalCount);
        Assert.True(second.pageInfo.hasPreviousPage);
        Assert.Equal(ConnectionHelper.SerializeCursor(2), second.pageInfo.startCursor);
        Assert.Equal(ConnectionHelper.SerializeCursor(3), second.pageInfo.endCursor);
        Assert.Equal(new[] { "B", "C" }, ((IEnumerable<dynamic>)second.edges).Select(e => (string)e.node.name));
        Assert.Equal(new[] { ConnectionHelper.SerializeCursor(2), ConnectionHelper.SerializeCursor(3) }, ((IEnumerable<dynamic>)second.edges).Select(e => (string)e.cursor));
    }

    [Fact]
    public void TestNestedConnectionPagingOnNavigationLastBefore()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = CreateActorWithMovies(factory);

        schema.Type<Actor>().ReplaceField("movies", a => a.Movies.OrderByDescending(m => m.Released), "Movies, newest first").UseConnectionPaging();
        var gql = new QueryRequest { Query = $@"{{ actor(id: 1) {{ movies(last: 1, before: ""{ConnectionHelper.SerializeCursor(2)}"") {{ edges {{ cursor node {{ name }} }} }} }} }}" };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic movies = ((dynamic)result.Data!["actor"]!).movies;
        Assert.Equal(1, Enumerable.Count(movies.edges));
        Assert.Equal("Newer", movies.edges[0].node.name);
        Assert.Equal(ConnectionHelper.SerializeCursor(1), movies.edges[0].cursor);
    }

    // the same Connection type paged at the root (IQueryable) and nested (a navigation) share the edges field
    [Fact]
    public void TestNestedAndRootConnectionPagingSameType()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = CreateActorWithMovies(factory);

        schema.Query().ReplaceField("movies", db => db.Movies.OrderByDescending(m => m.Released), "Movies").UseConnectionPaging();
        schema.Type<Actor>().ReplaceField("movies", a => a.Movies.OrderByDescending(m => m.Released), "Movies, newest first").UseConnectionPaging();
        var gql = new QueryRequest { Query = @"{ movies(first: 1) { edges { cursor node { name } } } actor(id: 1) { movies(first: 1) { edges { cursor node { name } } } } }" };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic root = result.Data!["movies"]!;
        Assert.Equal("Newer", root.edges[0].node.name);
        Assert.Equal(ConnectionHelper.SerializeCursor(1), root.edges[0].cursor);
        dynamic nested = ((dynamic)result.Data!["actor"]!).movies;
        Assert.Equal("Newer", nested.edges[0].node.name);
        Assert.Equal(ConnectionHelper.SerializeCursor(1), nested.edges[0].cursor);
    }

    // A before cursor shared by every parent can be past the end of a shorter collection. last: 3 before it is then the
    // last 3 of that collection - not a page that ends at the cursor and so misses rows
    [Fact]
    public void TestNestedConnectionPagingLastBeforePastTheEnd()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Actors.Add(new Actor("Long") { Id = 1, Movies = Enumerable.Range(1, 6).Select(i => new Movie($"L{i}") { Id = i, Released = new DateTime(2000 + i, 1, 1) }).ToList() });
        data.Actors.Add(new Actor("Short") { Id = 2, Movies = Enumerable.Range(1, 3).Select(i => new Movie($"S{i}") { Id = 10 + i, Released = new DateTime(2000 + i, 1, 1) }).ToList() });
        data.SaveChanges();

        schema.Type<Actor>().ReplaceField("movies", a => a.Movies.OrderBy(m => m.Released), "Movies").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                $@"{{ actors {{ name movies(last: 3, before: ""{ConnectionHelper.SerializeCursor(6)}"") {{ pageInfo {{ hasNextPage hasPreviousPage startCursor endCursor }} edges {{ cursor node {{ name }} }} }} }} }}",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic actors = result.Data!["actors"]!;
        dynamic longMovies = Enumerable.Single((IEnumerable<dynamic>)actors, a => a.name == "Long").movies;
        Assert.Equal(new[] { "L3", "L4", "L5" }, ((IEnumerable<dynamic>)longMovies.edges).Select(e => (string)e.node.name));
        Assert.Equal(new[] { 3, 4, 5 }.Select(ConnectionHelper.SerializeCursor), ((IEnumerable<dynamic>)longMovies.edges).Select(e => (string)e.cursor));
        Assert.True(longMovies.pageInfo.hasNextPage);
        Assert.True(longMovies.pageInfo.hasPreviousPage);

        dynamic shortMovies = Enumerable.Single((IEnumerable<dynamic>)actors, a => a.name == "Short").movies;
        Assert.Equal(new[] { "S1", "S2", "S3" }, ((IEnumerable<dynamic>)shortMovies.edges).Select(e => (string)e.node.name));
        Assert.Equal(new[] { 1, 2, 3 }.Select(ConnectionHelper.SerializeCursor), ((IEnumerable<dynamic>)shortMovies.edges).Select(e => (string)e.cursor));
        Assert.False(shortMovies.pageInfo.hasNextPage);
        Assert.False(shortMovies.pageInfo.hasPreviousPage);
        Assert.Equal(ConnectionHelper.SerializeCursor(1), shortMovies.pageInfo.startCursor);
        Assert.Equal(ConnectionHelper.SerializeCursor(3), shortMovies.pageInfo.endCursor);
    }

    [Fact]
    public void TestRootConnectionPagingLastBeforePastTheEnd()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = CreateActorWithMovies(factory);

        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Released), "Movies").UseConnectionPaging();
        var gql = new QueryRequest { Query = $@"{{ movies(last: 2, before: ""{ConnectionHelper.SerializeCursor(5)}"") {{ edges {{ cursor node {{ name }} }} }} }}" };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic movies = result.Data!["movies"]!;
        Assert.Equal(new[] { "Older", "Newer" }, ((IEnumerable<dynamic>)movies.edges).Select(e => (string)e.node.name));
        Assert.Equal(new[] { 1, 2 }.Select(ConnectionHelper.SerializeCursor), ((IEnumerable<dynamic>)movies.edges).Select(e => (string)e.cursor));
    }

    private static TestDbContext CreateActorWithMovies(TestDbContextFactory factory)
    {
        var data = factory.CreateContext();
        data.Actors.Add(
            new Actor("Actor") { Id = 1, Movies = [new Movie("Older") { Id = 1, Released = new DateTime(2020, 1, 1) }, new Movie("Newer") { Id = 2, Released = new DateTime(2021, 1, 1) }] }
        );
        data.SaveChanges();
        return data;
    }
}
