using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests.ConnectionPaging;

public class ConnectionPagingTests
{
    private static int peopleCnt;

    [Fact]
    public void TestGetsAll()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(data.People.Count, Enumerable.Count(people.edges));
        Assert.Equal(data.People.Count, people.totalCount);
        Assert.False(people.pageInfo.hasNextPage);
        Assert.False(people.pageInfo.hasPreviousPage);

        // cursors MQ, Mg, Mw, NA, NQ

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
        var expectedLastCursor = ConnectionHelper.SerializeCursor(Enumerable.Count(people.edges));
        Assert.Equal(expectedFirstCursor, people.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, people.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(people.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(people.edges).cursor);
    }

    [Fact]
    public void TestFirst()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(first: 1) {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(1, Enumerable.Count(people.edges));
        Assert.Equal(data.People.Count, people.totalCount);
        Assert.True(people.pageInfo.hasNextPage);
        Assert.False(people.pageInfo.hasPreviousPage);

        // cursors MQ, Mg, Mw, NA, NQ

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
        var expectedLastCursor = expectedFirstCursor;
        Assert.Equal(expectedFirstCursor, people.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, people.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(people.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(people.edges).cursor);
    }

    [Fact]
    public void TestFirstAfter()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(first: 2 after: ""MQ=="") {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, Enumerable.Count(people.edges));
        Assert.Equal(data.People.Count, people.totalCount);
        Assert.True(people.pageInfo.hasNextPage);
        Assert.True(people.pageInfo.hasPreviousPage);

        // cursors MQ, Mg, Mw, NA, NQ

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(2);
        var expectedLastCursor = ConnectionHelper.SerializeCursor(3);
        Assert.Equal(expectedFirstCursor, people.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, people.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(people.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(people.edges).cursor);
    }

    [Fact]
    public void TestLast()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(last: 2) {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, Enumerable.Count(people.edges));
        Assert.Equal(data.People.Count, people.totalCount);
        Assert.False(people.pageInfo.hasNextPage);
        Assert.True(people.pageInfo.hasPreviousPage);

        // cursors MQ, Mg, Mw, NA, NQ

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(4);
        var expectedLastCursor = ConnectionHelper.SerializeCursor(5);
        Assert.Equal(expectedFirstCursor, people.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, people.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(people.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(people.edges).cursor);
    }

    [Fact]
    public void TestLastBefore()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(last: 3 before: ""NA=="") {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(3, Enumerable.Count(people.edges));
        Assert.Equal(data.People.Count, people.totalCount);
        Assert.True(people.pageInfo.hasNextPage);
        Assert.False(people.pageInfo.hasPreviousPage);

        // cursors MQ, Mg, Mw, NA, NQ

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
        var expectedLastCursor = ConnectionHelper.SerializeCursor(3);
        Assert.Equal(expectedFirstCursor, people.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, people.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(people.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(people.edges).cursor);
    }

    [Fact]
    public void TestCursorFirstAfterMatchesLastBefore()
    {
        // Issue #427
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(first: 2 after: ""MQ=="") {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic firstPeople = result.Data!["people"]!;

        var gql2 = new QueryRequest
        {
            Query =
                @"{
                    people(last: 2 before: ""NA=="") {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        result = schema.ExecuteRequestWithContext(gql2, data, null, null);
        Assert.Null(result.Errors);

        dynamic lastPeople = result.Data!["people"]!;

        // cursors MQ, Mg, Mw, NA, NQ
        // first       Mg, Mw
        // last        Mg, Mw

        Assert.Equal(firstPeople.pageInfo.startCursor, lastPeople.pageInfo.startCursor);
        Assert.Equal(Enumerable.First(firstPeople.edges).cursor, Enumerable.First(lastPeople.edges).cursor);
        Assert.Equal(Enumerable.First(firstPeople.edges).node.id, Enumerable.First(lastPeople.edges).node.id);
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
            .UseConnectionPaging();

        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(first: 1, search: ""ill"") {
                        edges {
                            node {
                                name
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(1, Enumerable.Count(people.edges));
        Assert.Equal(2, people.totalCount); // 2 "ill" matches
        Assert.True(people.pageInfo.hasNextPage);
        Assert.False(people.pageInfo.hasPreviousPage);

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
        var expectedLastCursor = ConnectionHelper.SerializeCursor(1);
        Assert.Equal(expectedFirstCursor, people.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, people.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(people.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(people.edges).cursor);
    }

    [Fact]
    public void TestDefaultPageSize()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging(defaultPageSize: 2);
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic people = result.Data!["people"]!;
        Assert.Equal(2, Enumerable.Count(people.edges));
        Assert.Equal(data.People.Count, people.totalCount);
        Assert.True(people.pageInfo.hasNextPage);
        Assert.False(people.pageInfo.hasPreviousPage);

        // cursors MQ, Mg, Mw, NA, NQ

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
        var expectedLastCursor = ConnectionHelper.SerializeCursor(2);
        Assert.Equal(expectedFirstCursor, people.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, people.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(people.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(people.edges).cursor);
    }

    [Fact]
    public void TestMaxPageSizeFirst()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging(maxPageSize: 2);
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(first: 5) {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.NotNull(result.Errors);
        Assert.Equal("Field 'people' - first argument can not be greater than 2.", result.Errors[0].Message);
    }

    [Fact]
    public void TestMaxPageSizeLast()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata").UseConnectionPaging(maxPageSize: 2);
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(last: 5) {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.NotNull(result.Errors);
        Assert.Equal("Field 'people' - last argument can not be greater than 2.", result.Errors[0].Message);
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
                    people(first: 4) {
                        edges {
                            node {
                                name id
                            }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(4, Enumerable.Count(people.edges));
    }

    [Fact]
    public void TestOnNonRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata").UseConnectionPaging(defaultPageSize: 2);
        var gql = new QueryRequest
        {
            Query =
                @"{
                    projects {
                        name
                        tasks {
                            edges {
                                node {
                                    name id
                                }
                                cursor
                            }
                            pageInfo {
                                startCursor
                                endCursor
                                hasNextPage
                                hasPreviousPage
                            }
                            totalCount
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic projects = result.Data!["projects"]!;
        dynamic tasks = projects[0].tasks;
        Assert.Equal(2, Enumerable.Count(tasks.edges));
        Assert.Single(data.Projects);
        Assert.Equal(5, tasks.totalCount);
        Assert.True(tasks.pageInfo.hasNextPage);
        Assert.False(tasks.pageInfo.hasPreviousPage);

        // cursors MQ, Mg, Mw, NA, NQ

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
        var expectedLastCursor = ConnectionHelper.SerializeCursor(2);
        Assert.Equal(expectedFirstCursor, tasks.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, tasks.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(tasks.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(tasks.edges).cursor);
    }

    [Fact]
    public void TestOnNonRoot2()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata").UseConnectionPaging(defaultPageSize: 2);
        var gql = new QueryRequest
        {
            Query =
                @"{
                    project(id: 99) {
                        name
                        tasks {
                            edges {
                                node {
                                    name id
                                }
                                cursor
                            }
                            pageInfo {
                                startCursor
                                endCursor
                                hasNextPage
                                hasPreviousPage
                            }
                            totalCount
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic project = result.Data!["project"]!;
        Assert.Equal(2, Enumerable.Count(project.tasks.edges));
        Assert.Equal(5, project.tasks.totalCount);
        Assert.True(project.tasks.pageInfo.hasNextPage);
        Assert.False(project.tasks.pageInfo.hasPreviousPage);

        // cursors MQ, Mg, Mw, NA, NQ

        // we have tests for (de)serialization of cursor we're checking the correct ones are used
        var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
        var expectedLastCursor = ConnectionHelper.SerializeCursor(2);
        Assert.Equal(expectedFirstCursor, project.tasks.pageInfo.startCursor);
        Assert.Equal(expectedLastCursor, project.tasks.pageInfo.endCursor);
        Assert.Equal(expectedFirstCursor, Enumerable.First(project.tasks.edges).cursor);
        Assert.Equal(expectedLastCursor, Enumerable.Last(project.tasks.edges).cursor);
    }

    [Fact]
    public void TestPagingOnObjectProjectThatHasServiceField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema.Query().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata").UseConnectionPaging(defaultPageSize: 2);
        schema.UpdateType<Project>(type =>
        {
            type.AddField("lastUpdated", "Return last updated timestamp")
                // just need any service here to build the relation testing the use case
                .Resolve<AgeService>((project, ageSrv) => project.Updated == null ? DateTime.MinValue : new DateTime(ageSrv.GetAgeAsync(project.Updated).Result));
        });
        var gql = new QueryRequest
        {
            Query =
                @"{
                    tasks {
                        edges {
                            node {
                                project {
                                    lastUpdated
                                }
                            }
                        }
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        var ager = new AgeService();
        serviceCollection.AddSingleton(ager);
        var data = new TestDataContext();
        FillProjectData(data);

        var result = schema.ExecuteRequestWithContext(gql, data, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(result.Errors);
    }

    [Fact]
    public void TestPagingOnObjectProjectThatHasServiceField_WithAliases()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema.Query().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata").UseConnectionPaging(defaultPageSize: 2);
        schema.UpdateType<Project>(type =>
        {
            type.AddField("lastUpdated", "Return last updated timestamp")
                // just need any service here to build the relation testing the use case
                .Resolve<AgeService>((project, ageSrv) => project.Updated == null ? DateTime.MinValue : new DateTime(ageSrv.GetAgeAsync(project.Updated).Result));
        });
        var gql = new QueryRequest
        {
            Query =
                @"{
                    A: tasks {
                        B: edges {
                            C: node {
                                D: project {
                                    E: lastUpdated
                                }
                            }
                        }
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        var ager = new AgeService();
        serviceCollection.AddSingleton(ager);
        var data = new TestDataContext();
        FillProjectData(data);

        var result = schema.ExecuteRequestWithContext(gql, data, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(result.Errors);
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

        // This will create a ConnectionEdge<Person> type
        // the issue was the Field for ConnectionEdge<Person>.Items created once for that type has
        // UseArgumentsFromField set to peopleUnder when we want to query with peopleOver args
        // We can't share the ConnectionEdge<Person> type if the field has other arguments

        schema
            .Query()
            .AddField(
                "peopleOver",
                new { over = ArgumentHelper.Required<int>() },
                (ctx, args) => ctx.People.Where(p => p.Height > args.over).OrderBy(p => p.Id),
                "Return list of people with paging metadata"
            )
            .UseConnectionPaging();
        schema
            .Query()
            .AddField(
                "peopleUnder",
                new { under = ArgumentHelper.Required<int>() },
                (ctx, args) => ctx.People.Where(p => p.Height < args.under).OrderBy(p => p.Id),
                "Return list of people with paging metadata"
            )
            .UseConnectionPaging();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    peopleOver(over: 120, first: 1) {
                        totalCount
                        edges {
                            node {
                                name id height
                            }
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);
        dynamic results = result.Data!["peopleOver"]!;
        Assert.Equal(1, Enumerable.Count(results.edges));
        Assert.Equal(2, results.totalCount);
        Assert.Equal(200, results.edges[0].node.height);
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
                ],
            },
        ];
    }

    private static void FillData(TestDataContext data)
    {
        data.People = [MakePerson("Bill", "Murray"), MakePerson("John", "Frank"), MakePerson("Cheryl", "Crow"), MakePerson("Jill", "Castle"), MakePerson("Jack", "Snider")];
    }

    private static Person MakePerson(string fname, string lname)
    {
        return new Person
        {
            Id = peopleCnt++,
            Name = fname,
            LastName = lname,
        };
    }

    private class TestDataContext2 : TestDataContext
    {
        [UseConnectionPaging]
        public override List<Person> People { get; set; } = [];
    }

    [Fact]
    public void IdPropertyStillGenerated()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        Assert.Contains(schema.Query().GetFields(), x => x.Name == "person");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TestResolveWithServiceAndConnectionPaging(bool executeServiceFieldsSeparately)
    {
        // Issue #459 - Using UseConnectionPaging() with a resolver that depends on an injected service.
        // Requires ExecuteServiceFieldsSeparately = false because paging expressions like
        // ctx.People.Where(service.Filter) have interleaved service and context dependencies
        // that can't be split across the two-pass execution model.
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        // Set up a field with a service dependency and connection paging
        schema
            .Query()
            .ReplaceField("people", "Get a page of people")
            .Resolve<AgeService>((ctx, service) => ctx.People.Where(p => p.Birthday != null && service.GetAgeAsync(p.Birthday).Result > 0).OrderBy(p => p.Id))
            .UseConnectionPaging();

        var gql = new QueryRequest
        {
            Query =
                @"{
                    people {
                        edges {
                            node {
                                id name
                            }
                        }
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<AgeService>();

        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            serviceCollection.BuildServiceProvider(),
            null,
            new ExecutionOptions { ExecuteServiceFieldsSeparately = executeServiceFieldsSeparately }
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.NotNull(people.edges);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TestResolveWithServiceAndConnectionPagingWithArgs(bool executeServiceFieldsSeparately)
    {
        // Issue #459 - Variant with additional arguments alongside service.
        // Requires ExecuteServiceFieldsSeparately = false (see TestResolveWithServiceAndConnectionPaging).
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);

        // Set up a field with both arguments and a service dependency
        schema
            .Query()
            .ReplaceField("people", new { minAge = 0 }, "Get a page of people")
            .Resolve<AgeService>((ctx, args, service) => ctx.People.Where(p => p.Birthday != null && service.GetAgeAsync(p.Birthday).Result > args.minAge).OrderBy(p => p.Id))
            .UseConnectionPaging();

        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(minAge: 18) {
                        edges {
                            node {
                                id name
                            }
                        }
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<AgeService>();

        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            serviceCollection.BuildServiceProvider(),
            null,
            new ExecutionOptions { ExecuteServiceFieldsSeparately = executeServiceFieldsSeparately }
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.NotNull(people.edges);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TestResolveWithServiceAndConnectionPagingOnNonRoot(bool executeServiceFieldsSeparately)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema
            .Type<Project>()
            .ReplaceField("tasks", "Return list of task with paging metadata")
            .Resolve<TaskFilterService>((project, service) => project.Tasks.Where(t => service.IncludeTask(t.Id)).OrderBy(t => t.Id))
            .UseConnectionPaging(defaultPageSize: 2);

        var gql = new QueryRequest
        {
            Query =
                @"{
                    projects {
                        tasks {
                            edges {
                                node { id }
                            }
                            totalCount
                        }
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TaskFilterService>();

        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            serviceCollection.BuildServiceProvider(),
            null,
            new ExecutionOptions { ExecuteServiceFieldsSeparately = executeServiceFieldsSeparately }
        );

        Assert.Null(result.Errors);
        dynamic projects = result.Data!["projects"]!;
        dynamic tasks = projects[0].tasks;
        Assert.Equal(4, tasks.totalCount);
        Assert.Equal(2, Enumerable.Count(tasks.edges));
    }

    [Fact]
    public void TestMixedRootFields_ServicePagedAndNormal_WithSeparateServices()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);
        FillProjectData(data);
        for (var i = 0; i < data.People.Count; i++)
        {
            data.People[i].Birthday = DateTime.Now.AddYears(-(20 + i * 5));
        }

        schema
            .Query()
            .ReplaceField("people", "Get a page of people")
            .Resolve<AgeService>((ctx, service) => ctx.People.Where(p => p.Birthday != null && service.GetAgeAsync(p.Birthday).Result > 0).OrderBy(p => p.Id))
            .UseConnectionPaging();

        var gql = new QueryRequest
        {
            Query =
                @"{
                    people {
                        totalCount
                    }
                    projects {
                        id
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<AgeService>();

        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            serviceCollection.BuildServiceProvider(),
            null,
            new ExecutionOptions { ExecuteServiceFieldsSeparately = true }
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(5, people.totalCount);
        dynamic projects = result.Data!["projects"]!;
        Assert.Single(projects);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TestResolveWithServiceAndConnectionPaging_MinimalSelection(bool executeServiceFieldsSeparately)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillData(data);
        for (var i = 0; i < data.People.Count; i++)
        {
            data.People[i].Birthday = DateTime.Now.AddYears(-(20 + i * 5));
        }

        schema
            .Query()
            .ReplaceField("people", "Get a page of people")
            .Resolve<AgeService>((ctx, service) => ctx.People.Where(p => p.Birthday != null && service.GetAgeAsync(p.Birthday).Result > 0).OrderBy(p => p.Id))
            .UseConnectionPaging();

        var gql = new QueryRequest
        {
            Query =
                @"{
                    people {
                        totalCount
                        pageInfo {
                            hasNextPage
                        }
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<AgeService>();

        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            serviceCollection.BuildServiceProvider(),
            null,
            new ExecutionOptions { ExecuteServiceFieldsSeparately = executeServiceFieldsSeparately }
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(5, people.totalCount);
    }
}

public class TaskFilterService
{
    public bool IncludeTask(int taskId) => taskId > 0;
}
