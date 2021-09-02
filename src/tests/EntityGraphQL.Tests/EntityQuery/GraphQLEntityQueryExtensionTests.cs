using Xunit;
using System.Collections.Generic;
using System.Linq;
using static EntityGraphQL.Schema.ArgumentHelper;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Tests
{
    public class GraphQLEntityQueryExtensionTests
    {
        [Fact]
        public void SupportEntityQuery()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
            var gql = new QueryRequest
            {
                Query = @"query {
	users(filter: ""field2 == \""2\"" "") { field2 }
}",
            };
            var tree = schemaProvider.ExecuteQuery(gql, new TestDataContext().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }

        [Fact]
        public void SupportEntityQueryArgument()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
            var gql = new QueryRequest
            {
                Query = @"query {
                    users(filter: $filter) { field2 }
                }",
                Variables = new QueryVariables { { "filter", "field2 == \"2\"" } }
            };
            var tree = schemaProvider.ExecuteQuery(gql, new TestDataContext().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }

        [Fact]
        public void SupportUseFilter()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(false);
            schema.Type<TestDataContext>().GetField("users")
                .UseFilter();
            var gql = new QueryRequest
            {
                Query = @"query {
                    users(filter: $filter) { field2 }
                }",
                Variables = new QueryVariables { { "filter", "field2 == \"2\"" } }
            };
            var tree = schema.ExecuteQuery(gql, new TestDataContext().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }
        [Fact]
        public void SupportUseFilterOnNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(false);
            schema.Type<Project>().GetField("tasks")
                .UseFilter();
            var gql = new QueryRequest
            {
                Query = @"query {
                    projects {
                        tasks(filter: $filter) { id }
                    }
                }",
                Variables = new QueryVariables { { "filter", "(id == 2) || (id == 4)" } }
            };
            var tree = schema.ExecuteQuery(gql, new TestDataContext().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic projects = ((IDictionary<string, object>)tree.Data)["projects"];
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.First(projects);
            Assert.Equal(2, Enumerable.Count(project.tasks));
            Assert.Equal(2, Enumerable.ElementAt(project.tasks, 0).id);
            Assert.Equal(4, Enumerable.ElementAt(project.tasks, 1).id);
        }
        [Fact]
        public void TestAttribute()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext2>();

            var gql = new QueryRequest
            {
                Query = @"query {
                    people(filter: $filter) { name }
                }",
                Variables = new QueryVariables { { "filter", "name == \"Luke\"" } }
            };
            var tree = schema.ExecuteQuery(gql, (TestDataContext2)new TestDataContext2().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(1, Enumerable.Count(people));
            var user = Enumerable.First(people);
            Assert.Equal("Luke", user.name);
        }
        private class TestDataContext2 : TestDataContext
        {
            [UseFilter]
            public override List<Person> People { get; set; } = new List<Person>();
        }
    }
}