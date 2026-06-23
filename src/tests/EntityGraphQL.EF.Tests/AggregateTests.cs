using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class AggregateTests
{
    [Fact]
    public void PagingWrapperWithServiceFieldInSelection()
    {
        // A paged + aggregated field whose ELEMENT selection uses a service (config). The collection resolver
        // itself is pure DB, so the aggregate stays DB-side while the service field runs in the second pass.
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.AddType<ProjectConfig>("Config", "Config").AddAllFields();
        schema.Type<Movie>().AddField("config", "Get config").Resolve<ConfigService>((m, srv) => srv.Get(m.Id)).IsNullable(false);
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseOffsetPaging().UseAggregate(AggregatePlacement.PagingWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new ConfigService());
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Movies.AddRange(new Movie("A") { Id = 5 }, new Movie("B") { Id = 9 }, new Movie("C") { Id = 2 });
        data.SaveChanges();

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ movies { items { id config { type } } totalItems aggregate { count min { id } max { id } } } }" },
            data,
            services.BuildServiceProvider(),
            null
        );

        Assert.Null(result.Errors);
        dynamic movies = result.Data!["movies"]!;
        Assert.Equal(3, (int)movies.totalItems);
        Assert.Equal(3, (int)movies.aggregate.count);
        Assert.Equal(2, (int)movies.aggregate.min.id);
        Assert.Equal(9, (int)movies.aggregate.max.id);
    }

    [Fact]
    public void AggregateTranslatesToSql()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("movies", db => db.Movies, "Movies").UseAggregate(AggregatePlacement.SiblingField);

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Movies.AddRange(new Movie("A") { Id = 5 }, new Movie("B") { Id = 9 }, new Movie("C") { Id = 2 });
        data.SaveChanges();

        // sqlite throws if the aggregates can't be translated to SQL, so correct results prove translation
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ moviesAggregate { count min { id } max { id } sum { id } } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic agg = result.Data!["moviesAggregate"]!;
        Assert.Equal(3, (int)agg.count);
        Assert.Equal(2, (int)agg.min.id);
        Assert.Equal(9, (int)agg.max.id);
        Assert.Equal(16, (int)agg.sum.id);
    }

    [Fact]
    public void PagingWrapperAggregateTranslatesToSql()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseFilter().UseOffsetPaging().UseAggregate(AggregatePlacement.PagingWrapper);

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Movies.AddRange(new Movie("A") { Id = 5 }, new Movie("B") { Id = 9 }, new Movie("C") { Id = 2 });
        data.SaveChanges();

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = "query($filter: String!) { movies(filter: $filter) { totalItems items { id } aggregate { count min { id } max { id } } } }",
                Variables = new QueryVariables { { "filter", "id > 2" } },
            },
            data,
            null,
            null
        );

        Assert.Null(result.Errors);
        dynamic movies = result.Data!["movies"]!;
        Assert.Equal(2, (int)movies.totalItems);
        Assert.Equal(2, (int)movies.aggregate.count);
        Assert.Equal(5, (int)movies.aggregate.min.id);
        Assert.Equal(9, (int)movies.aggregate.max.id);
    }

    [Fact]
    public void ConnectionPagingWrapperAggregateTranslatesToSql()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().ReplaceField("movies", db => db.Movies.OrderBy(m => m.Id), "Movies").UseConnectionPaging().UseAggregate(AggregatePlacement.PagingWrapper);

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Movies.AddRange(new Movie("A") { Id = 5 }, new Movie("B") { Id = 9 }, new Movie("C") { Id = 2 });
        data.SaveChanges();

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies { totalCount edges { node { id } } aggregate { count min { id } max { id } } } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic movies = result.Data!["movies"]!;
        Assert.Equal(3, (int)movies.totalCount);
        Assert.Equal(3, (int)movies.aggregate.count);
        Assert.Equal(2, (int)movies.aggregate.min.id);
        Assert.Equal(9, (int)movies.aggregate.max.id);
    }
}
