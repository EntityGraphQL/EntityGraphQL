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

    /// <summary>Counts calls and the most running at once</summary>
    public class TrackingService
    {
        private int running;
        public int Calls;
        public int BulkCalls;
        public int MaxRunning;
        public System.Threading.CancellationTokenSource? CancelOnFirstCall;

        public async System.Threading.Tasks.Task<int> ScoreAsync(int id)
        {
            System.Threading.Interlocked.Increment(ref Calls);
            CancelOnFirstCall?.Cancel();
            var now = System.Threading.Interlocked.Increment(ref running);
            int max;
            while (now > (max = MaxRunning) && System.Threading.Interlocked.CompareExchange(ref MaxRunning, now, max) != max) { }
            await System.Threading.Tasks.Task.Delay(20);
            System.Threading.Interlocked.Decrement(ref running);
            return id * 10;
        }

        public int Score(int id) => id * 10;

        public IDictionary<int, int> Scores(IEnumerable<int> ids)
        {
            System.Threading.Interlocked.Increment(ref BulkCalls);
            return ids.ToDictionary(i => i, i => i * 10);
        }

        public async System.Threading.Tasks.Task<IDictionary<int, int>> ScoresAsync(IEnumerable<int> ids)
        {
            await System.Threading.Tasks.Task.Yield();
            return Scores(ids);
        }

        public System.Threading.Tasks.Task<int> ForAsync(Person p) => System.Threading.Tasks.Task.FromResult(p.Id);
    }

    private static TestDataContext People(int count) => new() { People = Enumerable.Range(1, count).Select(i => new Person { Id = i }).ToList() };

    private static (SchemaProvider<TestDataContext> schema, TrackingService svc, System.IServiceProvider services) Tracked(System.Action<SchemaProvider<TestDataContext>> addFields)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        addFields(schema);
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.OwnWrapper);
        var svc = new TrackingService();
        var services = new ServiceCollection();
        services.AddSingleton(svc);
        return (schema, svc, services.BuildServiceProvider());
    }

    [Fact]
    public void AsyncFieldWithSyncBulkResolverUsesTheBulkResolver()
    {
        var (schema, svc, services) = Tracked(s =>
            s.Type<Person>().AddField("score", "").ResolveAsync<TrackingService>((p, t) => t.ScoreAsync(p.Id)).ResolveBulk<TrackingService, int, int>(p => p.Id, (ids, t) => t.Scores(ids))
        );

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { aggregate { sum { score } max { score } } } }" }, People(4), services, null);

        Assert.Null(result.Errors);
        var agg = ((dynamic)result.Data!["people"]!).aggregate;
        Assert.Equal(100, (int)agg.sum.score);
        Assert.Equal(40, (int)agg.max.score);
        Assert.Equal(0, svc.Calls);
        Assert.Equal(1, svc.BulkCalls); // shared by the functions
    }

    [Fact]
    public void AsyncBulkResolverIsCalledOnce()
    {
        var (schema, svc, services) = Tracked(s =>
            s.Type<Person>().AddField("score", "").ResolveAsync<TrackingService>((p, t) => t.ScoreAsync(p.Id)).ResolveBulkAsync<TrackingService, int, int>(p => p.Id, (ids, t) => t.ScoresAsync(ids))
        );

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { aggregate { sum { score } max { score } average { score } } } }" }, People(50), services, null);

        Assert.Null(result.Errors);
        var agg = ((dynamic)result.Data!["people"]!).aggregate;
        Assert.Equal(12750, (int)agg.sum.score);
        Assert.Equal(500, (int)agg.max.score);
        Assert.Equal(255.0, (double)agg.average.score);
        Assert.Equal(0, svc.Calls);
        Assert.Equal(1, svc.BulkCalls);
    }

    [Fact]
    public void AsyncFieldRespectsItsConcurrencyLimitAcrossFunctions()
    {
        var (schema, svc, services) = Tracked(s => s.Type<Person>().AddField("score", "").ResolveAsync<TrackingService>((p, t) => t.ScoreAsync(p.Id), maxConcurrency: 1));

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { aggregate { sum { score } max { score } average { score } } } }" }, People(5), services, null);

        Assert.Null(result.Errors);
        Assert.Equal(150, (int)((dynamic)result.Data!["people"]!).aggregate.sum.score);
        Assert.Equal(5, svc.Calls); // the functions share the values
        Assert.Equal(1, svc.MaxRunning);
    }

    [Fact]
    public void AsyncFieldElementsAreAwaitedConcurrently()
    {
        var (schema, svc, services) = Tracked(s => s.Type<Person>().AddField("score", "").ResolveAsync<TrackingService>((p, t) => t.ScoreAsync(p.Id)));

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { aggregate { sum { score } } } }" }, People(20), services, null);

        Assert.Null(result.Errors);
        Assert.Equal(2100, (int)((dynamic)result.Data!["people"]!).aggregate.sum.score);
        Assert.True(svc.MaxRunning > 1, $"max running {svc.MaxRunning}");
    }

    [Fact]
    public async System.Threading.Tasks.Task AsyncFieldStopsWhenCancelled()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("score", "").ResolveAsync<TrackingService>((p, t) => t.ScoreAsync(p.Id), maxConcurrency: 1);
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.OwnWrapper);
        using var cts = new System.Threading.CancellationTokenSource();
        var svc = new TrackingService { CancelOnFirstCall = cts };
        var services = new ServiceCollection();
        services.AddSingleton(svc);
        services.AddSingleton(People(20));

        var result = await schema.ExecuteRequestAsync(new QueryRequest { Query = "{ people { aggregate { sum { score } } } }" }, services.BuildServiceProvider(), null, null, cts.Token);

        Assert.NotNull(result.Errors);
        // the request ends at the cancellation - give any calls still being made time to show up
        await System.Threading.Tasks.Task.Delay(300);
        Assert.Equal(1, svc.Calls);
    }

    [Fact]
    public void AsyncFieldWhoseServiceTakesTheElementIsNotAggregatable()
    {
        // pass 1 fetches the members a service reads, not the element itself
        var (schema, _, services) = Tracked(s =>
        {
            s.Type<Person>().AddField("score", "").ResolveAsync<TrackingService>((p, t) => t.ScoreAsync(p.Id));
            s.Type<Person>().AddField("whole", "").ResolveAsync<TrackingService>((p, t) => t.ForAsync(p));
        });

        Assert.True(schema.Type("PersonSumAggregate").HasField("score", null));
        Assert.False(schema.Type("PersonSumAggregate").HasField("whole", null));
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { aggregate { sum { score } } } }" }, People(2), services, null);
        Assert.Null(result.Errors);
    }

    public class MethodContext
    {
        public List<MethodThing> Things { get; set; } = [new MethodThing { Id = 1 }, new MethodThing { Id = 2 }];
    }

    public class MethodThing
    {
        public int Id { get; set; }

        [GraphQLField]
        public System.Threading.Tasks.Task<int> TaskScore([GraphQLService] TrackingService svc) => svc.ScoreAsync(Id);

        [GraphQLField]
        public async System.Threading.Tasks.ValueTask<int> ValueTaskScore([GraphQLService] TrackingService svc) => await svc.ScoreAsync(Id);

        [GraphQLField]
        public int SyncScore([GraphQLService] TrackingService svc) => svc.Score(Id);
    }

    [Fact]
    public void MethodFieldsWithServicesAreNotAggregatable()
    {
        // a method field is called on the element, which pass 1 does not fetch - they failed with "unbound variable"
        var schema = SchemaBuilder.FromObject<MethodContext>();
        schema.Query().ReplaceField("things", ctx => ctx.Things, "Things").UseAggregate(AggregatePlacement.OwnWrapper);

        var sumType = schema.Type("MethodThingSumAggregate");
        Assert.True(sumType.HasField("id", null));
        Assert.False(sumType.HasField("taskScore", null));
        Assert.False(sumType.HasField("valueTaskScore", null));
        Assert.False(sumType.HasField("syncScore", null));

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { aggregate { sum { id } } } }" }, new MethodContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(3, (int)((dynamic)result.Data!["things"]!).aggregate.sum.id);
    }

    [Fact]
    public void NestedAggregatesShareValuesOnlyWithinTheirCollection()
    {
        // each person's projects are their own collection - two people with the same project ids share, others do not
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Project>().AddField("score", "").ResolveAsync<TrackingService>((p, t) => t.ScoreAsync(p.Id));
        schema.Type<Person>().ReplaceField("projects", p => p.Projects, "").UseAggregate(AggregatePlacement.OwnWrapper);
        var svc = new TrackingService();
        var services = new ServiceCollection();
        services.AddSingleton(svc);
        var data = new TestDataContext
        {
            People =
            [
                new Person { Id = 1, Projects = [new Project { Id = 1 }, new Project { Id = 2 }] },
                new Person { Id = 2, Projects = [new Project { Id = 3 }] },
                new Person { Id = 3, Projects = [new Project { Id = 1 }, new Project { Id = 2 }] },
            ],
        };

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ people { id projects { aggregate { sum { score } max { score } } } } }" },
            data,
            services.BuildServiceProvider(),
            null
        );

        Assert.Null(result.Errors);
        var people = ((IEnumerable<dynamic>)result.Data!["people"]!).ToList();
        Assert.Equal(30, (int)people[0].projects.aggregate.sum.score);
        Assert.Equal(20, (int)people[0].projects.aggregate.max.score);
        Assert.Equal(30, (int)people[1].projects.aggregate.sum.score);
        Assert.Equal(30, (int)people[1].projects.aggregate.max.score);
        Assert.Equal(30, (int)people[2].projects.aggregate.sum.score);
        Assert.Equal(3, svc.Calls); // projects 1 and 2 once (people 1 and 3 share), project 3 once
    }
}
