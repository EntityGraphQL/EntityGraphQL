using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

// Aggregating a service-backed element field over EF: the deps (m.Id) are projected + materialized in pass 1
// (DB), then the service reduction runs in memory in pass 2.
public class ServiceAggregateTests
{
    public class ScoreService
    {
        public int Score(int id) => id * 10;
    }

    public class BulkScoreService
    {
        public int Score(int id) => id * 10;

        public System.Collections.Generic.IDictionary<int, int> GetScores(System.Collections.Generic.IEnumerable<int> ids) => ids.ToDictionary(id => id, id => id * 10);
    }

    [Fact]
    public void AggregatesBulkFieldOverEf()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema
            .Type<Movie>()
            .AddField("bulkScore", "bulk score")
            .Resolve<BulkScoreService>((m, svc) => svc.Score(m.Id))
            .ResolveBulk<BulkScoreService, int, int>(m => m.Id, (ids, svc) => svc.GetScores(ids));
        schema.Query().ReplaceField("movies", db => db.Movies, "Movies").UseAggregate(AggregatePlacement.OwnWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new BulkScoreService());
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Movies.AddRange(new Movie("A") { Id = 2 }, new Movie("B") { Id = 5 });
        data.SaveChanges();

        // pass 1 projects m.Id on the DB; pass 2 does one bulk fetch + reduces in memory
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies { aggregate { sum { bulkScore } max { bulkScore } } } }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        dynamic movies = result.Data!["movies"]!;
        dynamic agg = movies.aggregate;
        Assert.Equal(70, (int)agg.sum.bulkScore);
        Assert.Equal(50, (int)agg.max.bulkScore);
    }

    [Fact]
    public void OwnWrapperAggregatesServiceFieldOverEf()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Type<Movie>().AddField("score", "service score").Resolve<ScoreService>((m, svc) => svc.Score(m.Id));
        schema.Query().ReplaceField("movies", db => db.Movies, "Movies").UseAggregate(AggregatePlacement.OwnWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Movies.AddRange(new Movie("A") { Id = 2 }, new Movie("B") { Id = 5 });
        data.SaveChanges();

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ movies { aggregate { sum { score } max { score } } } }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        dynamic movies = result.Data!["movies"]!;
        dynamic agg = movies.aggregate;
        Assert.Equal(70, (int)agg.sum.score); // (2*10)+(5*10)
        Assert.Equal(50, (int)agg.max.score);
    }

    [Fact]
    public void MixedDbAndServiceAggregatesInOneQuery()
    {
        // count + sum.id are DB-translatable; sum.score / max.score need the service. Confirms one query runs
        // the DB aggregates + deps in pass 1, then the service reductions in memory in pass 2.
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Type<Movie>().AddField("score", "service score").Resolve<ScoreService>((m, svc) => svc.Score(m.Id));
        schema.Query().ReplaceField("movies", db => db.Movies, "Movies").UseAggregate(AggregatePlacement.OwnWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Movies.AddRange(new Movie("A") { Id = 2 }, new Movie("B") { Id = 5 });
        data.SaveChanges();

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ movies { aggregate { count sum { id score } max { id score } } } }" },
            data,
            services.BuildServiceProvider(),
            null
        );

        Assert.Null(result.Errors);
        dynamic movies = result.Data!["movies"]!;
        dynamic agg = movies.aggregate;
        Assert.Equal(2, (int)agg.count); // DB
        Assert.Equal(7, (int)agg.sum.id); // DB: 2+5
        Assert.Equal(5, (int)agg.max.id); // DB
        Assert.Equal(70, (int)agg.sum.score); // service: (2*10)+(5*10)
        Assert.Equal(50, (int)agg.max.score); // service
    }
}
