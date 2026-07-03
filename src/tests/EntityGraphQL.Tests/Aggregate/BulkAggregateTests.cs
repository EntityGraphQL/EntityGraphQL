using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests.Aggregate;

public class BulkAggregateTests
{
    public class BulkScoreService
    {
        public int PerElementCalls { get; private set; }
        public int BulkCalls { get; private set; }

        public int GetScore(int id)
        {
            PerElementCalls++;
            return id * 10;
        }

        public IDictionary<int, int> GetScores(IEnumerable<int> ids)
        {
            BulkCalls++;
            return ids.ToDictionary(id => id, id => id * 10);
        }
    }

    [Fact]
    public void AggregatingABulkField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Type<Person>()
            .AddField("bulkScore", "bulk score")
            .Resolve<BulkScoreService>((p, svc) => svc.GetScore(p.Id))
            .ResolveBulk<BulkScoreService, int, int>(p => p.Id, (ids, svc) => svc.GetScores(ids));
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.SiblingField);

        Assert.True(schema.Type("PersonSumAggregate").HasField("bulkScore", null));

        var svc = new BulkScoreService();
        var services = new ServiceCollection();
        services.AddSingleton(svc);
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ peopleAggregate { sum { bulkScore } max { bulkScore } } }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        dynamic agg = result.Data!["peopleAggregate"]!;
        Assert.Equal(70, (int)agg.sum.bulkScore); // (2*10)+(5*10)
        Assert.Equal(50, (int)agg.max.bulkScore);
        // the aggregate uses the BULK loader (batched), NOT the per-element fallback
        Assert.Equal(0, svc.PerElementCalls);
        Assert.True(svc.BulkCalls > 0);
    }
}
