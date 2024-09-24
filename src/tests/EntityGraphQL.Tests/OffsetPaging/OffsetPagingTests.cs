using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.OffsetPaging;

public class OffsetPagingTests
{
    private static int peopleCnt;

    [Fact]
    public void TestGetsAll()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(data.People.Count, Enumerable.Count(people.items));
        Assert.Equal(data.People.Count, people.totalItems);
        Assert.False(people.hasNextPage);
        Assert.False(people.hasPreviousPage);
    }

    [Fact]
    public void TestAliases()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    A: people {
                        B: items {
                            C: name D: id
                        }
                        E: hasNextPage
                        F: hasPreviousPage
                        H: totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["A"]!;
        Assert.Equal(data.People.Count, Enumerable.Count(people.B));
        Assert.Equal(data.People.Count, people.H);
        Assert.False(people.E);
        Assert.False(people.F);
    }

    [Fact]
    public void TestTake()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(take: 1) {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(1, Enumerable.Count(people.items));
        Assert.Equal(data.People.Count, people.totalItems);
        Assert.True(people.hasNextPage);
        Assert.False(people.hasPreviousPage);
    }

    [Fact]
    public void TestTakeSkip()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(take: 2 skip: 2) {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, Enumerable.Count(people.items));
        Assert.Equal(data.People.Count, people.totalItems);
        Assert.True(people.hasNextPage);
        Assert.True(people.hasPreviousPage);
    }

    [Fact]
    public void TestSkip()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(skip: 2) {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(3, Enumerable.Count(people.items));
        Assert.Equal(data.People.Count, people.totalItems);
        Assert.False(people.hasNextPage);
        Assert.True(people.hasPreviousPage);
    }

    [Fact]
    public void TestMergeArguments()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema
            .Query()
            .ReplaceField(
                "people",
                new { search = (string?)null },
                (ctx, args) => ctx.People.WhereWhen(p => p.Name.Contains(args.search!) || p.LastName.Contains(args.search!), !string.IsNullOrEmpty(args.search)).OrderBy(p => p.Id),
                "Return list of people with paging metadata"
            )
            .UseOffsetPaging();

        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(take: 1, search: ""ill"") {
                        items {
                            name
                        }
                        hasNextPage
                        hasPreviousPage
                        totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(1, Enumerable.Count(people.items));
        Assert.Equal(2, people.totalItems); // 2 "ill" matches
        Assert.True(people.hasNextPage);
        Assert.False(people.hasPreviousPage);
    }

    [Fact]
    public void TestDefaultPageSize()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseOffsetPaging(defaultPageSize: 3);
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(3, Enumerable.Count(people.items));
        Assert.Equal(data.People.Count, people.totalItems);
        Assert.True(people.hasNextPage);
        Assert.False(people.hasPreviousPage);
    }

    [Fact]
    public void TestMaxPageSize()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseOffsetPaging(maxPageSize: 2);
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(take: 3) {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.NotNull(result.Errors);
        Assert.Equal("Field 'people' - Argument take can not be greater than 2.", result.Errors[0].Message);
    }

    [Fact]
    public void TestAttribute()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var data = new TestDataContext2();
        FillData(data);

        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(take: 2) {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalItems
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, Enumerable.Count(people.items));
        Assert.Equal(data.People.Count, people.totalItems);
        Assert.True(people.hasNextPage);
        Assert.False(people.hasPreviousPage);
    }

    [Fact]
    public void TestOnNonRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema.Type<Project>().ReplaceField("tasks", project => project.Tasks.OrderBy(p => p.Id), "Return list of tasks with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    projects {
                        tasks(take: 1) {
                            items {
                                name id
                            }
                            hasNextPage
                            hasPreviousPage
                            totalItems
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic projects = result.Data!["projects"]!;
        var tasks = projects[0].tasks;
        Assert.Equal(5, tasks.totalItems);
        Assert.True(tasks.hasNextPage);
        Assert.False(tasks.hasPreviousPage);
    }

    [Fact]
    public void TestOnNonRoot2()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of tasks with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    project(id: 99) {
                        tasks(take: 1) {
                            items {
                                name id
                            }
                            hasNextPage
                            hasPreviousPage
                            totalItems
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic project = result.Data!["project"]!;
        var tasks = project.tasks;
        Assert.Equal(5, tasks.totalItems);
        Assert.True(tasks.hasNextPage);
        Assert.False(tasks.hasPreviousPage);
    }

    [Fact]
    public void TestMultiUseWithArgs()
    {
        // Issue #358
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        // make half short and half tall
        for (var i = 0; i < data.People.Count; i++)
        {
            data.People[i].Height = i % 2 == 0 ? 100 : 200;
        }

        // This will create a OffsetPage<Person> type
        // the issue was the Field for OffsetPage<Person>.Items created once for that type has
        // UseArgumentsFromField set to peopleUnder when we want to query with peopleOver args
        // We can't share the OffsetPage<Person> type if the field has other arguments

        schema
            .Query()
            .AddField(
                "peopleOver",
                new { over = ArgumentHelper.Required<int>() },
                (ctx, args) => ctx.People.Where(p => p.Height > args.over).OrderBy(p => p.Id),
                "Return list of people with paging metadata"
            )
            .UseOffsetPaging();
        schema
            .Query()
            .AddField(
                "peopleUnder",
                new { under = ArgumentHelper.Required<int>() },
                (ctx, args) => ctx.People.Where(p => p.Height < args.under).OrderBy(p => p.Id),
                "Return list of people with paging metadata"
            )
            .UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    peopleOver(over: 120, skip: 0, take: 1) {
                        hasPreviousPage
                        hasNextPage
                        totalItems
                        items {
                            name id height
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);
        dynamic results = result.Data!["peopleOver"]!;
        Assert.Equal(1, Enumerable.Count(results.items));
        Assert.Equal(2, results.totalItems);
        Assert.Equal(200, results.items[0].height);
    }

    [Fact]
    public void TestWithinAnotherPaging()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema.Query().GetField("projects", null).UseOffsetPaging();
        schema.Type<Project>().ReplaceField("tasks", project => project.Tasks.OrderBy(p => p.Id), "Return list of tasks with paging metadata").UseOffsetPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    projects {
                        totalItems
                        items {
                            id
                            tasks {
                                totalItems
                            }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic projects = result.Data!["projects"]!;
        Assert.Equal(1, projects.totalItems);
        var tasks = projects.items[0].tasks;
        Assert.Equal(5, tasks.totalItems);
    }

    private class TestDataContext2 : TestDataContext
    {
        [UseOffsetPaging]
        public override List<Person> People { get; set; } = [];
    }

    private static void FillData(TestDataContext data)
    {
        data.People = new() { MakePerson("Bill", "Murray"), MakePerson("John", "Frank"), MakePerson("Cheryl", "Crow"), MakePerson("Jill", "Castle"), MakePerson("Jack", "Snider"), };
    }

    private static void FillProjectData(TestDataContext data)
    {
        data.Projects =
        [
            new Project
            {
                Id = 99,
                Name = "Project 1",
                Tasks =
                [
                    new Task { Id = 0, Name = "Task 1" },
                    new Task { Id = 1, Name = "Task 2" },
                    new Task { Id = 2, Name = "Task 3" },
                    new Task { Id = 3, Name = "Task 4" },
                    new Task { Id = 4, Name = "Task 5" },
                ]
            }
        ];
    }

    private static Person MakePerson(string fname, string lname)
    {
        return new Person
        {
            Id = peopleCnt++,
            Name = fname,
            LastName = lname
        };
    }

    [Fact]
    public void IdPropertyStillGenerated()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        Assert.NotEmpty(schema.Query().GetFields().Where(x => x.Name == "person"));
    }
}
