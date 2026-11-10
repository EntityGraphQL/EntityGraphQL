using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests.Aggregate;

// Aggregating an async element field: the values its service needs are materialized in pass 1, then each element's
// value is awaited and reduced in pass 2.
public class AsyncAggregateTests
{
    public class ScoreService
    {
        public async System.Threading.Tasks.Task<int> ScoreAsync(int id)
        {
            await System.Threading.Tasks.Task.Yield();
            return id * 10;
        }

        public async System.Threading.Tasks.Task<int?> MaybeScoreAsync(int id)
        {
            await System.Threading.Tasks.Task.Yield();
            return id == 5 ? null : id;
        }

        public async System.Threading.Tasks.Task<IDictionary<int, int>> ScoresAsync(IEnumerable<int> ids)
        {
            await System.Threading.Tasks.Task.Yield();
            return ids.ToDictionary(i => i, i => i * 10);
        }
    }

    private static SchemaProvider<TestDataContext> MakeSchema(AggregatePlacement placement)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("score", "").ResolveAsync<ScoreService>((p, svc) => svc.ScoreAsync(p.Id));
        schema.Type<Person>().AddField("maybeScore", "").ResolveAsync<ScoreService>((p, svc) => svc.MaybeScoreAsync(p.Id));
        // the bulk resolver is async, so the aggregate awaits the per-element resolver
        schema
            .Type<Person>()
            .AddField("bulkScore", "")
            .ResolveAsync<ScoreService>((p, svc) => svc.ScoreAsync(p.Id))
            .ResolveBulkAsync<ScoreService, int, int>(p => p.Id, (ids, svc) => svc.ScoresAsync(ids));
        var people = schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "People");
        if (placement == AggregatePlacement.PagingWrapper)
            people.UseOffsetPaging();
        people.UseAggregate(placement);
        return schema;
    }

    private static readonly string Selection = "count sum { score maybeScore bulkScore } average { score } min { score maybeScore } max { score bulkScore }";

    [Theory]
    [InlineData(AggregatePlacement.OwnWrapper, false)]
    [InlineData(AggregatePlacement.OwnWrapper, true)]
    [InlineData(AggregatePlacement.SiblingField, false)]
    [InlineData(AggregatePlacement.PagingWrapper, false)]
    public void AggregatesAsyncFields(AggregatePlacement placement, bool cacheCompiledDelegates)
    {
        var schema = MakeSchema(placement);
        Assert.True(schema.Type("PersonSumAggregate").HasField("score", null));

        var query = placement switch
        {
            AggregatePlacement.SiblingField => $"{{ peopleAggregate {{ {Selection} }} }}",
            AggregatePlacement.PagingWrapper => $"{{ people {{ totalItems aggregate {{ {Selection} }} }} }}",
            _ => $"{{ people {{ aggregate {{ {Selection} }} }} }}",
        };
        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }, new Person { Id = 8 }] };
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = query },
            data,
            services.BuildServiceProvider(),
            null,
            new ExecutionOptions { EnableQueryCache = cacheCompiledDelegates, CacheCompiledDelegates = cacheCompiledDelegates }
        );

        Assert.Null(result.Errors);
        dynamic agg = placement == AggregatePlacement.SiblingField ? result.Data!["peopleAggregate"]! : ((dynamic)result.Data!["people"]!).aggregate;
        Assert.Equal(3, (int)agg.count);
        Assert.Equal(150, (int)agg.sum.score);
        Assert.Equal(10, (int)agg.sum.maybeScore); // nulls are skipped, as Sum does
        Assert.Equal(150, (int)agg.sum.bulkScore);
        Assert.Equal(50.0, (double)agg.average.score);
        Assert.Equal(20, (int)agg.min.score);
        Assert.Equal(2, (int)agg.min.maybeScore);
        Assert.Equal(80, (int)agg.max.score);
        Assert.Equal(80, (int)agg.max.bulkScore);
    }

    [Fact]
    public void AggregatesAsyncFieldsOverEmptySet()
    {
        var schema = MakeSchema(AggregatePlacement.OwnWrapper);
        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ people { aggregate { count sum { score } min { score } average { score } } } }" },
            new TestDataContext(),
            services.BuildServiceProvider(),
            null
        );

        Assert.Null(result.Errors);
        dynamic agg = ((dynamic)result.Data!["people"]!).aggregate;
        Assert.Equal(0, (int)agg.count);
        Assert.Equal(0, (int)agg.sum.score);
        Assert.Null(agg.min.score);
        Assert.Null(agg.average.score);
    }

    public class ThingContext
    {
        public List<Thing> Things { get; set; } = [new Thing { Id = 1 }, new Thing { Id = 2 }];
    }

    public class Thing
    {
        public int Id { get; set; }

        [GraphQLField]
        public System.Threading.Tasks.Task<int> DoubledAsync() => System.Threading.Tasks.Task.FromResult(Id * 2);
    }

    [Fact]
    public void AsyncFieldWithoutServiceIsNotAggregatable()
    {
        // with no service it would be reduced in the database pass, where nothing awaits it
        var schema = SchemaBuilder.FromObject<ThingContext>();
        schema.Query().ReplaceField("things", ctx => ctx.Things, "Things").UseAggregate(AggregatePlacement.OwnWrapper);

        Assert.True(schema.Type("ThingSumAggregate").HasField("id", null));
        Assert.False(schema.Type("ThingSumAggregate").HasField("doubledAsync", null));

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { aggregate { sum { id } } } }" }, new ThingContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(3, (int)((dynamic)result.Data!["things"]!).aggregate.sum.id);
    }
}
