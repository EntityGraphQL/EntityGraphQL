using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static EntityGraphQL.Tests.ServiceFieldTests;

namespace EntityGraphQL.Tests.ConnectionPaging
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

            schema.ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people(first: 2, sort: { name: ASC }, filter: ""lastName == \""Frank\"""") {
                        edges {
                            node {
                                name id lastName
                            }
                        }
                        totalCount
                    }
                }",
            };

            var result = schema.ExecuteQuery(gql, data, null, null);
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

            schema.ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people(take: 2, sort: { name: ASC }, filter: ""lastName == \""Frank\"""") {
                        items {
                            name id lastName
                        }
                        totalItems
                    }
                }",
            };

            var result = schema.ExecuteQuery(gql, data, null, null);
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

            schema.ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseOffsetPaging();
            schema.Type<Person>().AddField("age",
                // use a filed not another relation/entity
                (person) => ArgumentHelper.WithService((AgeService ager) => ager.GetAge(person.Birthday)),
                "Persons age");
            var gql = new QueryRequest
            {
                Query = @"{
                    people(take: 2, sort: { name: ASC }, filter: ""lastName == \""Frank\"""") {
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

            var result = schema.ExecuteQuery(gql, data, serviceCollection.BuildServiceProvider(), null);
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

            schema.ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseConnectionPaging();
            schema.Type<Person>().AddField("age",
                // use a filed not another relation/entity
                (person) => ArgumentHelper.WithService((AgeService ager) => ager.GetAge(person.Birthday)),
                "Persons age");
            var gql = new QueryRequest
            {
                Query = @"{
                    people(first: 2, sort: { name: ASC }, filter: ""lastName == \""Frank\"""") {
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

            var result = schema.ExecuteQuery(gql, data, serviceCollection.BuildServiceProvider(), null);
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

        [Fact(Skip = "Coming soon")]
        public void TestConnectionPagingWithOthersAndServicesNonRoot()
        {
            // TODO
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.ReplaceField("people", ctx => ctx.People, "Return list of people with paging metadata")
                .UseFilter()
                .UseSort()
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    peossdple(first: 2, sort: { name: ASC }, filter: ""lastName == \""Frank\"""") {
                        edges {
                            node {
                                name id lastName
                            }
                        }
                        totalCount
                    }
                }",
            };

            var result = schema.ExecuteQuery(gql, data, null, null);
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