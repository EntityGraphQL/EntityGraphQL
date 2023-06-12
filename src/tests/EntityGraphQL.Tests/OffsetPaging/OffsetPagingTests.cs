using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;
using System.Collections.Generic;

namespace EntityGraphQL.Tests.OffsetPaging
{
    public class OffsetPagingTests
    {
        private static int peopleCnt;

        [Fact]
        public void TestGetsAll()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["A"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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
            .UseOffsetPaging();

            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging(defaultPageSize: 3);
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging(maxPageSize: 2);
            var gql = new QueryRequest
            {
                Query = @"{
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
                Query = @"{
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

            dynamic people = result.Data["people"];
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

            schema.Type<Project>().ReplaceField("tasks", project => project.Tasks.OrderBy(p => p.Id), "Return list of tasks with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic projects = result.Data["projects"];
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

            schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of tasks with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
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

            dynamic project = result.Data["project"];
            var tasks = project.tasks;
            Assert.Equal(5, tasks.totalItems);
            Assert.True(tasks.hasNextPage);
            Assert.False(tasks.hasPreviousPage);
        }
        private class TestDataContext2 : TestDataContext
        {
            [UseOffsetPaging]
            public override List<Person> People { get; set; } = new List<Person>();
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
}