using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static EntityGraphQL.Tests.ServiceFieldTests;

namespace EntityGraphQL.Tests
{
    public class FieldExtensionTests
    {
        private static int peopleCnt;

        [Fact]
        public void TestConnectionPagingWithOthers()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people(first: 2, sort: [{ name: ASC }], filter: ""lastName == \""Frank\"""") {
                        edges {
                            node {
                                name id lastName
                            }
                        }
                        totalCount
                    }
                }",
            };

            var result = schema.ExecuteRequestWithContext(gql, data, null, null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(2, Enumerable.Count(people.edges));
            // filtered
            Assert.Equal(3, people.totalCount);
            var person1 = Enumerable.ElementAt(people.edges, 0);
            var person2 = Enumerable.ElementAt(people.edges, 1);
            Assert.Equal("Frank", person1.node.lastName);
            Assert.Equal("Frank", person2.node.lastName);
            Assert.Equal("Cheryl", person1.node.name);
            Assert.Equal("Jill", person2.node.name);
        }

        [Fact]
        public void TestOffsetPagingWithOthers()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people(take: 2, sort: [{ name: ASC }], filter: ""lastName == \""Frank\"""") {
                        items {
                            name id lastName
                        }
                        totalItems
                    }
                }",
            };

            var result = schema.ExecuteRequestWithContext(gql, data, null, null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(2, Enumerable.Count(people.items));
            // filtered
            Assert.Equal(3, people.totalItems);
            var person1 = Enumerable.ElementAt(people.items, 0);
            var person2 = Enumerable.ElementAt(people.items, 1);
            Assert.Equal("Frank", person1.lastName);
            Assert.Equal("Frank", person2.lastName);
            Assert.Equal("Cheryl", person1.name);
            Assert.Equal("Jill", person2.name);
        }

        [Fact]
        public void TestOffsetPagingWithOthersAndServices()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseOffsetPaging();
            schema.Type<Person>().AddField("age", "Persons age")
                .ResolveWithService<AgeService>(
                    // use a filed not another relation/entity
                    (person, ager) => ager.GetAge(person.Birthday)
                );
            var gql = new QueryRequest
            {
                Query = @"{
                    people(take: 2, sort: [{ name: ASC }], filter: ""lastName == \""Frank\"""") {
                        items {
                            name id age lastName
                        }
                        totalItems
                    }
                }",
            };

            var serviceCollection = new ServiceCollection();
            var ager = new AgeService();
            serviceCollection.AddSingleton(ager);

            var result = schema.ExecuteRequestWithContext(gql, data, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(2, Enumerable.Count(people.items));
            // filtered
            Assert.Equal(3, people.totalItems);
            var person1 = Enumerable.ElementAt(people.items, 0);
            var person2 = Enumerable.ElementAt(people.items, 1);
            Assert.Equal("Frank", person1.lastName);
            Assert.Equal("Frank", person2.lastName);
            Assert.Equal("Cheryl", person1.name);
            Assert.Equal("Jill", person2.name);
        }

        [Fact]
        public void TestConnectionPagingWithOthersAndServices()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseConnectionPaging();
            schema.Type<Person>().AddField("age", "Persons age")
                .ResolveWithService<AgeService>(
                    // use a filed not another relation/entity
                    (person, ager) => ager.GetAge(person.Birthday)
                );
            var gql = new QueryRequest
            {
                Query = @"{
                    people(first: 2, sort: [{ name: ASC }], filter: ""lastName == \""Frank\"""") {
                        edges {
                            node {
                                name id lastName age
                            }
                            cursor
                        }
                        totalCount
                    }
                }",
            };

            var serviceCollection = new ServiceCollection();
            var ager = new AgeService();
            serviceCollection.AddSingleton(ager);

            var result = schema.ExecuteRequestWithContext(gql, data, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(2, Enumerable.Count(people.edges));
            // filtered
            Assert.Equal(3, people.totalCount);
            var person1 = Enumerable.ElementAt(people.edges, 0);
            var person2 = Enumerable.ElementAt(people.edges, 1);
            Assert.Equal("Frank", person1.node.lastName);
            Assert.Equal("Frank", person2.node.lastName);
            Assert.Equal("Cheryl", person1.node.name);
            Assert.Equal("Jill", person2.node.name);
        }

        [Fact]
        public void TestConnectionPagingWithOthersAndServicesNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            data.FillWithTestData();

            schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata")
                .UseFilter()
                .UseSort()
                .UseConnectionPaging(defaultPageSize: 2);
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();
            schema.Type<Task>().AddField("config", "Task config")
                .ResolveWithService<ConfigService>((t, srv) => srv.Get(t.Id));
            var gql = new QueryRequest
            {
                Query = @"{
                    projects {
                        name
                        tasks(filter: ""id < 4"" sort: [{ id: DESC }]) {
                            edges {
                                node {
                                    name id
                                    config { type }
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

            var serviceCollection = new ServiceCollection();
            var ager = new ConfigService();
            serviceCollection.AddSingleton(ager);

            var result = schema.ExecuteRequestWithContext(gql, data, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(result.Errors);

            dynamic projects = result.Data["projects"];
            dynamic tasks = projects[0].tasks;
            Assert.Equal(2, Enumerable.Count(tasks.edges));
            Assert.Equal(3, tasks.totalCount); // filtered 1 out
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
            // sort
            Assert.Equal(3, Enumerable.First(tasks.edges).node.id);
            Assert.Equal(2, Enumerable.Last(tasks.edges).node.id); // not 1 as we paged the results
        }

        private static void FillData(TestDataContext data)
        {
            data.People = new()
            {
                MakePerson("Bill", "Murray"),
                MakePerson("John", "Frank"),
                MakePerson("Cheryl", "Frank"),
                MakePerson("Jill", "Frank"),
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
    }
}