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
}
