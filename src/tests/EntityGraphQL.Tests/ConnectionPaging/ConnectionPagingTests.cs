using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Tests.ConnectionPaging
{
    public class ConnectionPagingTests
    {
        private static int peopleCnt;

        [Fact]
        public void TestGetsAll()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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
        public void TestMergeArguments()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.Query().ReplaceField(
                "people",
                new
                {
                    search = (string)null
                },
                (ctx, args) => ctx.People
                    .WhereWhen(p => p.Name.Contains(args.search) || p.LastName.Contains(args.search), !string.IsNullOrEmpty(args.search))
                    .OrderBy(p => p.Id),
                "Return list of people with paging metadata")
            .UseConnectionPaging();

            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging(defaultPageSize: 2);
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging(maxPageSize: 2);
            var gql = new QueryRequest
            {
                Query = @"{
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging(maxPageSize: 2);
            var gql = new QueryRequest
            {
                Query = @"{
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
                Query = @"{
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
            dynamic people = result.Data["people"];
            Assert.Equal(4, Enumerable.Count(people.edges));
        }

        [Fact]
        public void TestOnNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillProjectData(data);

            schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata")
                .UseConnectionPaging(defaultPageSize: 2);
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic projects = result.Data["projects"];
            dynamic tasks = projects[0].tasks;
            Assert.Equal(2, Enumerable.Count(tasks.edges));
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

            schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata")
                .UseConnectionPaging(defaultPageSize: 2);
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic project = result.Data["project"];
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

            schema.Query().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata")
                .UseConnectionPaging(defaultPageSize: 2);
            schema.UpdateType<Project>(type =>
            {
                type.AddField("lastUpdated", "Return last updated timestamp")
                    // just need any service here to build the relation testing the use case
                    .ResolveWithService<AgeService>((project, ageSrv) => project.Updated == null ? DateTime.MinValue : new DateTime(ageSrv.GetAgeAsync(project.Updated).Result));
            });
            var gql = new QueryRequest
            {
                Query = @"{
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

            schema.Query().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata")
                .UseConnectionPaging(defaultPageSize: 2);
            schema.UpdateType<Project>(type =>
            {
                type.AddField("lastUpdated", "Return last updated timestamp")
                    // just need any service here to build the relation testing the use case
                    .ResolveWithService<AgeService>((project, ageSrv) => project.Updated == null ? DateTime.MinValue : new DateTime(ageSrv.GetAgeAsync(project.Updated).Result));
            });
            var gql = new QueryRequest
            {
                Query = @"{
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

        private static void FillProjectData(TestDataContext data)
        {
            data.Projects = new List<Project>
            {
                new Project
                {
                    Id = 99,
                    Name ="Project 1",
                    Tasks = new List<Task>
                    {
                        new Task
                        {
                            Id = 0,
                            Name = "Task 1"
                        },
                        new Task
                        {
                            Id = 1,
                            Name = "Task 2"
                        },
                        new Task
                        {
                            Id = 2,
                            Name = "Task 3"
                        },
                        new Task
                        {
                            Id = 3,
                            Name = "Task 4"
                        },
                        new Task
                        {
                            Id = 4,
                            Name = "Task 5"
                        },

                    }
                }
            };
        }

        private static void FillData(TestDataContext data)
        {
            data.People = new()
            {
                MakePerson("Bill", "Murray"),
                MakePerson("John", "Frank"),
                MakePerson("Cheryl", "Crow"),
                MakePerson("Jill", "Castle"),
                MakePerson("Jack", "Snider"),
            };
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

        private class TestDataContext2 : TestDataContext
        {
            [UseConnectionPaging]
            public override List<Person> People { get; set; } = new List<Person>();
        }

        [Fact]
        public void IdPropertyStillGenerated()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext2>();
            Assert.NotEmpty(schema.Query().GetFields().Where(x => x.Name == "person"));
        }
    }
}