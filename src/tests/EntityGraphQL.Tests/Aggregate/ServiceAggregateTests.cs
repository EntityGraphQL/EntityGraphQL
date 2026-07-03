using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests.Aggregate;

// Aggregating a service-backed element field (model B): the per-element service values are projected/materialized
// in pass 1, then reduced in memory in pass 2.
public class ServiceAggregateTests
{
    public class ScoreService
    {
        public int Calls { get; private set; }

        public int Score(int id)
        {
            Calls++;
            return id * 10;
        }
    }

    [Fact]
    public void OwnWrapperAggregatesServiceField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("score", "service score").Resolve<ScoreService>((p, svc) => svc.Score(p.Id));
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.OwnWrapper);

        // a service-backed element field is now aggregatable
        Assert.True(schema.Type("PersonSumAggregate").HasField("score", null));

        var svc = new ScoreService();
        var services = new ServiceCollection();
        services.AddSingleton(svc);
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { aggregate { sum { score } max { score } } } }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        dynamic agg = people.aggregate;
        Assert.Equal(70, (int)agg.sum.score); // (2*10)+(5*10)
        Assert.Equal(50, (int)agg.max.score);
    }

    [Fact]
    public void SiblingAggregatesServiceField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("score", "service score").Resolve<ScoreService>((p, svc) => svc.Score(p.Id));
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.SiblingField);

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ peopleAggregate { sum { score } } }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        dynamic agg = result.Data!["peopleAggregate"]!;
        Assert.Equal(70, (int)agg.sum.score);
    }

    [Fact]
    public void PagingWrapperAggregatesServiceField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("score", "service score").Resolve<ScoreService>((p, svc) => svc.Score(p.Id));
        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "People").UseOffsetPaging().UseAggregate(AggregatePlacement.PagingWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { totalItems aggregate { sum { score } } } }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(70, (int)people.aggregate.sum.score);
    }

    [Fact]
    public void ServiceAggregateRespectsFilter()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("score", "service score").Resolve<ScoreService>((p, svc) => svc.Score(p.Id));
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseFilter().UseAggregate(AggregatePlacement.OwnWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = "query($f: String!) { people(filter: $f) { aggregate { sum { score } } } }",
                Variables = new QueryVariables { { "f", "id > 3" } },
            },
            data,
            services.BuildServiceProvider(),
            null
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(50, (int)people.aggregate.sum.score); // only id=5 passes the filter
    }

    [Fact]
    public void ServiceAggregateOverEmptySet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("score", "service score").Resolve<ScoreService>((p, svc) => svc.Score(p.Id));
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseFilter().UseAggregate(AggregatePlacement.OwnWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = "query($f: String!) { people(filter: $f) { aggregate { count sum { score } max { score } } } }",
                Variables = new QueryVariables { { "f", "id > 100" } },
            },
            data,
            services.BuildServiceProvider(),
            null
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(0, (int)people.aggregate.count);
        Assert.Equal(0, (int)people.aggregate.sum.score); // sum of empty = 0
        Assert.Null(people.aggregate.max.score); // max of empty = null
    }
}
