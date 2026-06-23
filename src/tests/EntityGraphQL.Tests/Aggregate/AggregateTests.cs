using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.Aggregate;

public class AggregateTests
{
    private class ShortContext
    {
        public List<ShortThing> Things { get; set; } = [];
    }

    private class ShortThing
    {
        public int Id { get; set; }
        public short Level { get; set; }
    }

    [Fact]
    public void SupportsShortFields()
    {
        var schema = SchemaBuilder.FromObject<ShortContext>();
        schema.Query().ReplaceField("things", ctx => ctx.Things, "things").UseAggregate(AggregatePlacement.SiblingField);

        // short gets all four functions (sum/average widened to int/double)
        Assert.True(schema.Type("ShortThingSumAggregate").HasField("level", null));
        Assert.True(schema.Type("ShortThingAverageAggregate").HasField("level", null));

        var data = new ShortContext { Things = [new() { Level = 3 }, new() { Level = 7 }] };
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ thingsAggregate { sum { level } average { level } min { level } max { level } } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic agg = result.Data!["thingsAggregate"]!;
        Assert.Equal(10, (int)agg.sum.level);
        Assert.Equal(5.0, (double)agg.average.level);
        Assert.Equal((short)3, (short)agg.min.level);
        Assert.Equal((short)7, (short)agg.max.level);
    }

    private static TestDataContext MakeData() => new() { People = [new Person { Id = 1, Height = 180 }, new Person { Id = 2, Height = 160 }, new Person { Id = 4, Height = 200 }] };

    [Fact]
    public void AddsSiblingAggregateField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.SiblingField);

        Assert.True(schema.Query().HasField("peopleAggregate", null));
        Assert.True(schema.HasType("PersonAggregate"));
        Assert.True(schema.HasType("PersonMinAggregate"));
        Assert.True(schema.HasType("PersonMaxAggregate"));
        Assert.True(schema.HasType("PersonSumAggregate"));
        Assert.True(schema.HasType("PersonAverageAggregate"));
    }

    [Fact]
    public void CountAndNumericAggregates()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.SiblingField);

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ peopleAggregate { count min { height id } max { height id } sum { height } average { height } } }" },
            MakeData(),
            null,
            null
        );

        Assert.Null(result.Errors);
        dynamic agg = result.Data!["peopleAggregate"]!;
        Assert.Equal(3, (int)agg.count);
        Assert.Equal(160.0, (double)agg.min.height);
        Assert.Equal(200.0, (double)agg.max.height);
        Assert.Equal(1, (int)agg.min.id);
        Assert.Equal(4, (int)agg.max.id);
        Assert.Equal(540.0, (double)agg.sum.height);
        Assert.Equal(180.0, (double)agg.average.height);
    }

    [Fact]
    public void PagingWrapperAttachesAggregateToPage()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "People").UseOffsetPaging().UseAggregate(AggregatePlacement.PagingWrapper);

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { items { id } totalItems aggregate { count min { id } max { id } } } }" }, MakeData(), null, null);

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(3, (int)people.totalItems);
        Assert.Equal(3, Enumerable.Count(people.items));
        Assert.Equal(3, (int)people.aggregate.count);
        Assert.Equal(1, (int)people.aggregate.min.id);
        Assert.Equal(4, (int)people.aggregate.max.id);
    }

    [Fact]
    public void AutoPlacementUsesPagingWrapperWhenPaged()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "People").UseOffsetPaging().UseAggregate();

        // aggregate is on the page type, not a sibling field
        Assert.False(schema.Query().HasField("peopleAggregate", null));
        Assert.True(schema.Type("PersonPage").HasField("aggregate", null));
    }

    [Fact]
    public void PagingWrapperAggregateRespectsFilter()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "People").UseFilter().UseOffsetPaging().UseAggregate(AggregatePlacement.PagingWrapper);

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = "query($filter: String!) { people(filter: $filter) { totalItems aggregate { count max { id } } } }",
                Variables = new QueryVariables { { "filter", "id > 1" } },
            },
            MakeData(),
            null,
            null
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, (int)people.totalItems);
        Assert.Equal(2, (int)people.aggregate.count);
        Assert.Equal(4, (int)people.aggregate.max.id);
    }

    [Fact]
    public void OwnWrapperExposesItemsAndAggregate()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.OwnWrapper);

        Assert.True(schema.HasType("PersonWithAggregate"));
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { items { id } aggregate { count max { id } } } }" }, MakeData(), null, null);

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(3, Enumerable.Count(people.items));
        Assert.Equal(3, (int)people.aggregate.count);
        Assert.Equal(4, (int)people.aggregate.max.id);
    }

    [Fact]
    public void OwnWrapperAggregateRespectsFilter()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseFilter().UseAggregate(AggregatePlacement.OwnWrapper);

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = "query($filter: String!) { people(filter: $filter) { items { id } aggregate { count min { id } } } }",
                Variables = new QueryVariables { { "filter", "id > 1" } },
            },
            MakeData(),
            null,
            null
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, Enumerable.Count(people.items));
        Assert.Equal(2, (int)people.aggregate.count);
        Assert.Equal(2, (int)people.aggregate.min.id);
    }

    [Fact]
    public void SiblingAggregateRespectsFilter()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseFilter().UseAggregate(AggregatePlacement.SiblingField);

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = "query($filter: String!) { peopleAggregate(filter: $filter) { count min { id } } }",
                Variables = new QueryVariables { { "filter", "id > 1" } },
            },
            MakeData(),
            null,
            null
        );

        Assert.Null(result.Errors);
        dynamic agg = result.Data!["peopleAggregate"]!;
        Assert.Equal(2, (int)agg.count);
        Assert.Equal(2, (int)agg.min.id);
    }

    [Fact]
    public void FieldSelectionRestrictsAggregatableFields()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate((Person p) => new { p.Height }, AggregatePlacement.SiblingField);

        // only height is aggregatable, id is excluded
        Assert.True(schema.Type("PersonMinAggregate").HasField("height", null));
        Assert.False(schema.Type("PersonMinAggregate").HasField("id", null));

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ peopleAggregate { count min { height } } }" }, MakeData(), null, null);
        Assert.Null(result.Errors);
        dynamic agg = result.Data!["peopleAggregate"]!;
        Assert.Equal(160.0, (double)agg.min.height);
    }

    [Fact]
    public void FieldSelectionSingleMember()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate((Person p) => p.Height);

        Assert.True(schema.Type("PersonSumAggregate").HasField("height", null));
        Assert.False(schema.Type("PersonSumAggregate").HasField("id", null));
    }

    [Fact]
    public void RejectsMutationField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var mutationField = schema.Mutation().Add("makePeople", () => System.Array.Empty<Person>());
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => mutationField.UseAggregate());
        Assert.Contains("Mutation", ex.Message);
    }

    [Fact]
    public void NonRootCollectionFieldSibling()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().ReplaceField("projects", p => p.Projects, "projects").UseAggregate(AggregatePlacement.SiblingField);

        var data = new TestDataContext { People = [new Person { Id = 1, Projects = [new Project { Id = 10 }, new Project { Id = 20 }] }] };
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { id projectsAggregate { count min { id } max { id } } } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        dynamic person = Enumerable.First(people);
        Assert.Equal(2, (int)person.projectsAggregate.count);
        Assert.Equal(10, (int)person.projectsAggregate.min.id);
        Assert.Equal(20, (int)person.projectsAggregate.max.id);
    }

    [Fact]
    public void OriginalFieldStillWorks()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate(AggregatePlacement.SiblingField);

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { id } peopleAggregate { count } }" }, MakeData(), null, null);

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(3, Enumerable.Count(people));
        dynamic agg = result.Data!["peopleAggregate"]!;
        Assert.Equal(3, (int)agg.count);
    }
}
