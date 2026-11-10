using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests.Aggregate;

public class AggregateServiceAndArgsTests
{
    public class PeopleService
    {
        public int Calls { get; private set; }

        public IEnumerable<Person> Get()
        {
            Calls++;
            return [new Person { Id = 1, Height = 180 }, new Person { Id = 2, Height = 160 }, new Person { Id = 4, Height = 200 }];
        }
    }

    public class KeepService
    {
        public bool Keep(int id) => id > 1;
    }

    public class ScoreService
    {
        public int Score(int id) => id * 10;
    }

    [Fact]
    public void ServiceComputedFieldsAreAggregatable()
    {
        // a service-backed FIELD on the element type is aggregatable (computed via the two-pass reduction)
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("score", "service score").Resolve<ScoreService>((p, svc) => svc.Score(p.Id));
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate();

        Assert.True(schema.Type("PersonSumAggregate").HasField("score", null));
        Assert.True(schema.Type("PersonSumAggregate").HasField("id", null));
    }

    [Fact]
    public void AggregateOverEmptySet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseFilter().UseAggregate(AggregatePlacement.OwnWrapper);

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = "query($f: String!) { people(filter: $f) { items { id } aggregate { count min { id } max { id } average { id } } } }",
                Variables = new QueryVariables { { "f", "id > 100" } },
            },
            MakeData(),
            null,
            null
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(0, (int)people.aggregate.count);
        Assert.Null(people.aggregate.min.id);
        Assert.Null(people.aggregate.max.id);
        Assert.Null(people.aggregate.average.id);
    }

    private static TestDataContext MakeData() => new() { People = [new Person { Id = 1 }, new Person { Id = 2 }, new Person { Id = 4 }] };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OffsetPagingWrapperWithInterleavedService(bool executeServiceFieldsSeparately)
    {
        // interleaved service in the COLLECTION resolver (ctx.People.Where(p => svc.Keep(...))) + paging + aggregate.
        // This is the case the paging two-pass machinery handles; the aggregate must run over the same filtered set.
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Query()
            .ReplaceField("people", "People")
            .Resolve<KeepService>((ctx, svc) => ctx.People.Where(p => svc.Keep(p.Id)).OrderBy(p => p.Id))
            .UseOffsetPaging()
            .UseAggregate(AggregatePlacement.PagingWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new KeepService());
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ people { items { id } totalItems aggregate { count min { id } max { id } } } }" },
            MakeData(),
            services.BuildServiceProvider(),
            null,
            new ExecutionOptions { ExecuteServiceFieldsSeparately = executeServiceFieldsSeparately }
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, (int)people.totalItems);
        Assert.Equal(2, (int)people.aggregate.count);
        Assert.Equal(2, (int)people.aggregate.min.id);
        Assert.Equal(4, (int)people.aggregate.max.id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConnectionPagingWrapperWithInterleavedService(bool executeServiceFieldsSeparately)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Query()
            .ReplaceField("people", "People")
            .Resolve<KeepService>((ctx, svc) => ctx.People.Where(p => svc.Keep(p.Id)).OrderBy(p => p.Id))
            .UseConnectionPaging()
            .UseAggregate(AggregatePlacement.PagingWrapper);

        var services = new ServiceCollection();
        services.AddSingleton(new KeepService());
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ people { totalCount edges { node { id } } aggregate { count min { id } max { id } } } }" },
            MakeData(),
            services.BuildServiceProvider(),
            null,
            new ExecutionOptions { ExecuteServiceFieldsSeparately = executeServiceFieldsSeparately }
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, (int)people.totalCount);
        Assert.Equal(2, (int)people.aggregate.count);
        Assert.Equal(2, (int)people.aggregate.min.id);
        Assert.Equal(4, (int)people.aggregate.max.id);
    }

    [Fact]
    public void OwnWrapperOverServiceField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("peopleSvc", "People from a service").Resolve<PeopleService>((ctx, svc) => svc.Get()).UseAggregate(AggregatePlacement.OwnWrapper);

        var svc = new PeopleService();
        var services = new ServiceCollection();
        services.AddSingleton(svc);
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ peopleSvc { items { id } aggregate { count max { id } } } }" },
            new TestDataContext(),
            services.BuildServiceProvider(),
            null
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["peopleSvc"]!;
        Assert.Equal(3, Enumerable.Count(people.items));
        Assert.Equal(3, (int)people.aggregate.count);
        Assert.Equal(4, (int)people.aggregate.max.id);
        // critical: the service must be materialized once, not re-invoked per aggregate leaf
        Assert.Equal(1, svc.Calls);
    }

    [Fact]
    public void OwnWrapperWithCustomArgs()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("peeps", new { minId = 0 }, (ctx, args) => ctx.People.Where(p => p.Id >= args.minId), "People over a min id").UseAggregate(AggregatePlacement.OwnWrapper);

        var data = new TestDataContext { People = [new Person { Id = 1 }, new Person { Id = 2 }, new Person { Id = 4 }] };
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ peeps(minId: 2) { items { id } aggregate { count min { id } } } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic people = result.Data!["peeps"]!;
        Assert.Equal(2, Enumerable.Count(people.items));
        Assert.Equal(2, (int)people.aggregate.count);
        Assert.Equal(2, (int)people.aggregate.min.id);
    }

    [Fact]
    public void AutoUsesOwnWrapperForServiceField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("peopleSvc", "People from a service").Resolve<PeopleService>((ctx, svc) => svc.Get()).UseAggregate();

        Assert.False(schema.Query().HasField("peopleSvcAggregate", null));
        Assert.True(schema.Type("PersonWithAggregate").HasField("aggregate", null));
    }

    [Fact]
    public void AutoUsesOwnWrapperForUnpagedField()
    {
        // Auto defaults to OwnWrapper for an unpaged field (handles services/custom args uniformly). SiblingField is opt-in.
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("peeps", new { minId = 0 }, (ctx, args) => ctx.People.Where(p => p.Id >= args.minId), "People over a min id").UseAggregate();

        Assert.False(schema.Query().HasField("peepsAggregate", null));
        Assert.True(schema.HasType("PersonWithAggregate"));
    }

    [Fact]
    public void SiblingCarriesCustomArgs()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("peeps", new { minId = 0 }, (ctx, args) => ctx.People.Where(p => p.Id >= args.minId), "People").UseAggregate(AggregatePlacement.SiblingField);

        var data = new TestDataContext { People = [new Person { Id = 1 }, new Person { Id = 2 }, new Person { Id = 4 }] };
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ peepsAggregate(minId: 2) { count min { id } } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic agg = result.Data!["peepsAggregate"]!;
        Assert.Equal(2, (int)agg.count);
        Assert.Equal(2, (int)agg.min.id);
    }

    [Fact]
    public void SiblingWithServiceThrows()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() =>
            schema.Query().AddField("peopleSvc", "svc").Resolve<PeopleService>((ctx, svc) => svc.Get()).UseAggregate(AggregatePlacement.SiblingField)
        );
        Assert.Contains("service-backed", ex.Message);
    }
}
