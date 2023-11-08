using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.AggregateExtensionTests;

public class AggregateExtensionTests
{
    [Fact]
    public void TestGetsAllAtRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        data.People.Add(new Person { Height = 184 });
        data.People.Add(new Person { Height = 175 });
        data.People.Add(new Person { Height = 163 });
        data.People.Add(new Person { Height = 167 });

        schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people")
            .UseAggregate();

        var gql = new QueryRequest
        {
            Query = @"{
                    peopleAggregate {
                        count
                        heightMin
                        heightMax
                        heightAverage
                        heightSum
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic peopleAggregate = result.Data["peopleAggregate"];
        Assert.Equal(4, peopleAggregate.count);
        Assert.Equal(163, peopleAggregate.heightMin);
        Assert.Equal(184, peopleAggregate.heightMax);
        Assert.Equal(172.25, peopleAggregate.heightAverage);
        Assert.Equal(689, peopleAggregate.heightSum);
    }

    [Fact]
    public void TestGetsAllAtNonRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        data.Projects.Add(new Project
        {
            Name = "Project 1",
            Tasks = new List<Task>
            {
                new Task { Name = "Task 1", HoursEstimated = 1 },
                new Task { Name = "Task 2", HoursEstimated = 2 },
                new Task { Name = "Task 3", HoursEstimated = 3 },
                new Task { Name = "Task 4", HoursEstimated = 4 },
            }
        });

        // Project.Tasks has [UseAggregate]

        var gql = new QueryRequest
        {
            Query = @"{
                    projects {
                        tasksAggregate {
                            count
                            hoursEstimatedMin
                            hoursEstimatedMax
                            hoursEstimatedAverage
                            hoursEstimatedSum
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic taskAggregate = ((dynamic)result.Data["projects"])[0].tasksAggregate;
        Assert.Equal(4, taskAggregate.count);
        Assert.Equal(1, taskAggregate.hoursEstimatedMin);
        Assert.Equal(4, taskAggregate.hoursEstimatedMax);
        Assert.Equal(2.5, taskAggregate.hoursEstimatedAverage);
        Assert.Equal(10, taskAggregate.hoursEstimatedSum);
    }

    [Fact]
    public void TestRenameField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        data.People.Add(new Person { Height = 184 });
        data.People.Add(new Person { Height = 175 });
        data.People.Add(new Person { Height = 163 });
        data.People.Add(new Person { Height = 167 });

        schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people")
            .UseAggregate("aggregatePeeps");

        var gql = new QueryRequest
        {
            Query = @"{
                    aggregatePeeps {
                        count
                        heightMin
                        heightMax
                        heightAverage
                        heightSum
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic peopleAggregate = result.Data["aggregatePeeps"];
        Assert.Equal(4, peopleAggregate.count);
        Assert.Equal(163, peopleAggregate.heightMin);
        Assert.Equal(184, peopleAggregate.heightMax);
        Assert.Equal(172.25, peopleAggregate.heightAverage);
        Assert.Equal(689, peopleAggregate.heightSum);
    }

    [Fact]
    public void TestOnlyIncludeCertainFields()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();

        schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people")
            // only include height field
            .UseAggregate(new string[] { "height" });

        Assert.Empty(schema.GetSchemaType("QueryPeopleAggregate", null).GetFields().Where(f => f.Name == "idSum" || f.Name == "idMin" || f.Name == "idMax" || f.Name == "idAverage"));
        Assert.Empty(schema.GetSchemaType("QueryPeopleAggregate", null).GetFields().Where(f => f.Name == "birthdayMin" || f.Name == "birthdayMax"));
        Assert.Equal(4, schema.GetSchemaType("QueryPeopleAggregate", null).GetFields().Where(f => f.Name == "heightSum" || f.Name == "heightMin" || f.Name == "heightMax" || f.Name == "heightAverage").Count());
    }

    [Fact]
    public void TestOnlyExcludeCertainFields()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();

        schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people")
            // exclude height field
            .UseAggregate(new string[] { "height" }, true);

        Assert.Equal(4, schema.GetSchemaType("QueryPeopleAggregate", null).GetFields().Where(f => f.Name == "idSum" || f.Name == "idMin" || f.Name == "idMax" || f.Name == "idAverage").Count());
        Assert.Empty(schema.GetSchemaType("QueryPeopleAggregate", null).GetFields().Where(f => f.Name == "heightSum" || f.Name == "heightMin" || f.Name == "heightMax" || f.Name == "heightAverage"));
        Assert.Equal(2, schema.GetSchemaType("QueryPeopleAggregate", null).GetFields().Where(f => f.Name == "birthdayMin" || f.Name == "birthdayMax").Count());
    }

    [Fact]
    public void TestDifferentOptionsOnSameTypeDifferentFields()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();

        schema.Query().ReplaceField("tasks", ctx => ctx.Tasks, "Return list of tasks")
            .UseAggregate(new string[] { "hoursEstimated" });

        schema.UpdateType<Person>(type =>
        {
            type.ReplaceField("tasks", ctx => ctx.Tasks, "Return list of tasks")
                .UseAggregate(new string[] { "hoursCompleted" });
        });

        Assert.Equal(6, schema.GetSchemaType("QueryTasksAggregate", null).GetFields().Count());
        Assert.Equal(4, schema.GetSchemaType("QueryTasksAggregate", null).GetFields().Where(f => f.Name == "hoursEstimatedSum" || f.Name == "hoursEstimatedMin" || f.Name == "hoursEstimatedMax" || f.Name == "hoursEstimatedAverage").Count());
        Assert.Equal(6, schema.GetSchemaType("PersonTasksAggregate", null).GetFields().Count());
        Assert.Equal(4, schema.GetSchemaType("PersonTasksAggregate", null).GetFields().Where(f => f.Name == "hoursCompletedSum" || f.Name == "hoursCompletedMin" || f.Name == "hoursCompletedMax" || f.Name == "hoursCompletedAverage").Count());
    }

    [Fact]
    public void TestIncludeAttribute()
    {
        var schema = SchemaBuilder.FromObject<TestDataContextExtended>();
        var data = new TestDataContextExtended();

        schema.Query().ReplaceField("SomeEntities", ctx => ctx.SomeEntities, "Return list of SomeEntities");

        Assert.Empty(schema.GetSchemaType("QuerySomeEntitiesAggregate", null).GetFields().Where(f => f.Name == "idSum" || f.Name == "idMin" || f.Name == "idMax" || f.Name == "idAverage"));
        Assert.Empty(schema.GetSchemaType("QuerySomeEntitiesAggregate", null).GetFields().Where(f => f.Name == "birthdayMin" || f.Name == "birthdayMax"));
        Assert.Equal(4, schema.GetSchemaType("QuerySomeEntitiesAggregate", null).GetFields().Where(f => f.Name == "heightSum" || f.Name == "heightMin" || f.Name == "heightMax" || f.Name == "heightAverage").Count());
    }

    [Fact]
    public void TestExcludeAttribute()
    {
        var schema = SchemaBuilder.FromObject<TestDataContextExtended>();
        var data = new TestDataContextExtended();

        schema.Query().ReplaceField("OtherEntities", ctx => ctx.OtherEntities, "Return list of OtherEntities");

        Assert.Empty(schema.GetSchemaType("QueryOtherEntitiesAggregate", null).GetFields().Where(f => f.Name == "idSum" || f.Name == "idMin" || f.Name == "idMax" || f.Name == "idAverage"));
        Assert.Empty(schema.GetSchemaType("QueryOtherEntitiesAggregate", null).GetFields().Where(f => f.Name == "heightSum" || f.Name == "heightMin" || f.Name == "heightMax" || f.Name == "heightAverage"));
        Assert.Equal(2, schema.GetSchemaType("QueryOtherEntitiesAggregate", null).GetFields().Where(f => f.Name == "birthdayMin" || f.Name == "birthdayMax").Count());
    }

    [Fact]
    public void TestUsesSchemaFieldName()
    {
        var schema = SchemaBuilder.FromObject<TestDataContextExtended>();
        var data = new TestDataContextExtended();

        Assert.Single(schema.Query().GetFields().Where(f => f.Name == "renamedAggregate"));
    }
}

public class TestDataContextExtended : TestDataContext
{
    [UseAggregate(AutoAddFields = false)] // force use of [IncludeAggregateField]
    public IEnumerable<EntityWithInclude> SomeEntities { get; set; } = new List<EntityWithInclude>();

    [UseAggregate]
    public IEnumerable<EntityWithExclude> OtherEntities { get; set; } = new List<EntityWithExclude>();
    [UseAggregate]
    [GraphQLField("renamed")]
    public IEnumerable<EntityWithExclude> OtherEntities2 { get; set; } = new List<EntityWithExclude>();
}

public class EntityWithExclude
{
    [ExcludeAggregateField]
    public int Id { get; set; }
    public DateTime Birthday { get; set; }
    [ExcludeAggregateField]
    public float Height { get; set; }
}

public class EntityWithInclude
{
    public int Id { get; set; }
    public DateTime Birthday { get; set; }
    [IncludeAggregateField]
    public float Height { get; set; }
}