using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Newtonsoft.Json;

namespace EntityGraphQL.Tests
{
    public class SortTests
    {
        [Fact]
        public void SupportUseSort()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(false);
            schema.Type<TestDataContext>().GetField("people")
                .UseSort();
            var gql = new QueryRequest
            {
                Query = @"query($sort: PersonSortInput) {
                    people(sort: $sort) { lastName }
                }",
                Variables = JsonConvert.DeserializeObject<QueryVariables>("{ \"sort\": { \"lastName\": \"DESC\" } }")
            };
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo"
            });
            var tree = schema.ExecuteQuery(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Zoo", person.lastName);
        }
        [Fact]
        public void TestAttribute()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext2>();

            var gql = new QueryRequest
            {
                Query = @"query($sort: PersonSortInput) {
                    people(sort: $sort) { lastName }
                }",
                Variables = JsonConvert.DeserializeObject<QueryVariables>("{ \"sort\": { \"lastName\": \"DESC\" } }")
            };
            TestDataContext2 context = new TestDataContext2();
            context.FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo"
            });
            var tree = schema.ExecuteQuery(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Zoo", person.lastName);
        }
        private class TestDataContext2 : TestDataContext
        {
            [UseSort]
            public override List<Person> People { get; set; } = new List<Person>();
        }

        [Fact]
        public void SupportUseSortSelectFields()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(false);
            schema.Type<TestDataContext>().GetField("people")
                .UseSort((Person person) => new
                {
                    person.Height,
                    person.Name
                });
            var gql = new QueryRequest
            {
                Query = @"query($sort: PersonSortInput) {
                    people(sort: $sort) { lastName }
                }",
                Variables = JsonConvert.DeserializeObject<QueryVariables>("{ \"sort\": { \"height\": \"ASC\" } }")
            };
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo",
                Height = 1
            });
            var tree = schema.ExecuteQuery(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Zoo", person.lastName);
            var schemaType = schema.Type("PeopleSortInput");
            var fields = schemaType.GetFields().ToList();
            Assert.Equal(3, fields.Count);
            Assert.Equal("__typename", fields[0].Name);
            Assert.Equal("height", fields[1].Name);
            Assert.Equal("name", fields[2].Name);
        }
        [Fact]
        public void SupportUseSortOnNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(false);
            schema.Type<Project>().GetField("tasks")
                .UseSort();
            var gql = new QueryRequest
            {
                Query = @"query($sort: PersonSortInput) {
                    projects {
                        tasks(sort: $sort) { id }
                    }
                }",
                Variables = JsonConvert.DeserializeObject<QueryVariables>("{ \"sort\": { \"id\": \"DESC\" } }")
            };
            var context = new TestDataContext().FillWithTestData();
            var tree = schema.ExecuteQuery(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic projects = ((IDictionary<string, object>)tree.Data)["projects"];
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.First(projects);
            Assert.Equal(4, Enumerable.Count(project.tasks));
            Assert.Equal(4, Enumerable.ElementAt(project.tasks, 0).id);
            Assert.Equal(3, Enumerable.ElementAt(project.tasks, 1).id);
            Assert.Equal(2, Enumerable.ElementAt(project.tasks, 2).id);
            Assert.Equal(1, Enumerable.ElementAt(project.tasks, 3).id);
        }
        [Fact]
        public void SupportUseSortOnNonRoot2()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<Project>().GetField("tasks")
                .UseSort();
            var gql = new QueryRequest
            {
                Query = @"query($sort: PersonSortInput) {
                    project(id: 55) {
                        tasks(sort: $sort) { id }
                    }
                }",
                Variables = JsonConvert.DeserializeObject<QueryVariables>("{ \"sort\": { \"id\": \"DESC\" } }")
            };
            var context = new TestDataContext().FillWithTestData();
            var tree = schema.ExecuteQuery(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic project = ((IDictionary<string, object>)tree.Data)["project"];
            Assert.Equal(4, Enumerable.Count(project.tasks));
            Assert.Equal(4, Enumerable.ElementAt(project.tasks, 0).id);
            Assert.Equal(3, Enumerable.ElementAt(project.tasks, 1).id);
            Assert.Equal(2, Enumerable.ElementAt(project.tasks, 2).id);
            Assert.Equal(1, Enumerable.ElementAt(project.tasks, 3).id);
        }
    }

    internal class TestArgs
    {
        public SortDirectionEnum? lastName { get; set; }
    }
}