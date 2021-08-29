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
    }

    internal class TestArgs
    {
        public SortDirectionEnum? lastName { get; set; }
    }
}