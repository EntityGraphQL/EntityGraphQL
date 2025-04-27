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
