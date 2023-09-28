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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
            var gql = new QueryRequest
            {
                Query = @"query {
	users(filter: ""field2 == \""2\"" "") { field2 }
}",
            };
            var context = new TestDataContext().FillWithTestData();
            context.Users.Add(new User { Field2 = "99" });
            var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }

        [Fact]
        public void SupportEntityQueryEmptyString()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
            var gql = new QueryRequest
            {
                Query = @"query {
	users(filter: """") { field2 }
}",
            };
            var context = new TestDataContext().FillWithTestData();
            context.Users.Add(new User { Field2 = "99" });
            var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(2, Enumerable.Count(users));
        }

        [Fact]
        public void SupportEntityQueryStringWhitespace()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
            var gql = new QueryRequest
            {
                Query = @"query {
	users(filter: ""  "") { field2 }
}",
            };
            var context = new TestDataContext().FillWithTestData();
            context.Users.Add(new User { Field2 = "99" });
            var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(2, Enumerable.Count(users));
        }

        [Fact]
        public void SupportEntityQueryArgument()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
            var gql = new QueryRequest
            {
                Query = @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
                Variables = new QueryVariables { { "filter", "field2 == \"2\"" } }
            };
            var context = new TestDataContext().FillWithTestData();
            context.Users.Add(new User { Field2 = "99" });
            var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }

        [Fact]
        public void FilterExpressionWithNoValue()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
            var gql = new QueryRequest
            {
                Query = @"query Query($filter: String) {
                    users(filter: $filter) { field2 }
                }",
                // do not pass any values. p.filter.HasValue will be false (and work)
                Variables = new QueryVariables()
            };
            var context = new TestDataContext().FillWithTestData();
            context.Users.Add(new User { Field2 = "99" });
            var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(2, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
            user = Enumerable.ElementAt(users, 1);
            Assert.Equal("99", user.field2);
        }

        [Fact]
        public void FilterExpressionWithNoValueNoDocVar()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
            var gql = new QueryRequest
            {
                Query = @"query Query() {
                    users { field2 }
                }",
                // do not pass any values. p.filter.HasValue will be false (and work)
                Variables = new QueryVariables()
            };
            var context = new TestDataContext().FillWithTestData();
            context.Users.Add(new User { Field2 = "99" });
            var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(2, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
            user = Enumerable.ElementAt(users, 1);
            Assert.Equal("99", user.field2);
        }

        [Fact]
        public void SupportUseFilter()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<TestDataContext>().GetField("users", null)
                .UseFilter();
            var gql = new QueryRequest
            {
                Query = @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
                Variables = new QueryVariables { { "filter", "field2 == \"2\"" } }
            };
            var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }

        [Fact]
        public void SupportUseFilterWithOrStatement()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
            schema.Type<TestDataContext>().GetField("users", null)
                .UseFilter();
            var gql = new QueryRequest
            {
                Query = @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
                Variables = new QueryVariables { { "filter", "field2 == \"2\" or field2 == \"3\"" } }
            };
            var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }

        [Fact]
        public void SupportUseFilterWithAndStatement()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
            schema.Type<TestDataContext>().GetField("users", null)
                .UseFilter();
            var gql = new QueryRequest
            {
                Query = @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
                Variables = new QueryVariables { { "filter", "field2 == \"2\" and field2 == \"2\"" } }
            };
            var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }

        [Fact]
        public void SupportUseFilterWithnotEqualStatement()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
            schema.Type<TestDataContext>().GetField("users", null)
                .UseFilter();
            var gql = new QueryRequest
            {
                Query = @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
                Variables = new QueryVariables { { "filter", "field2 != \"3\"" } }
            };
            var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic users = ((IDictionary<string, object>)tree.Data)["users"];
            Assert.Equal(1, Enumerable.Count(users));
            var user = Enumerable.First(users);
            Assert.Equal("2", user.field2);
        }

        [Fact]
        public void SupportUseFilterOnNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<Project>().GetField("tasks", null)
                .UseFilter();
            var gql = new QueryRequest
            {
                Query = @"query Query($filter: String!) {
                    projects {
                        tasks(filter: $filter) { id }
                    }
                }",
                Variables = new QueryVariables { { "filter", "(id == 2) || (id == 4)" } }
            };
            var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
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
                Query = @"query Query($filter: String!) {
                    people(filter: $filter) { name }
                }",
                Variables = new QueryVariables { { "filter", "name == \"Luke\"" } }
            };
            var tree = schema.ExecuteRequestWithContext(gql, (TestDataContext2)new TestDataContext2().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(1, Enumerable.Count(people));
            var user = Enumerable.First(people);
            Assert.Equal("Luke", user.name);
        }

        [Fact]
        public void TestTrueConstant()
        {
            // came from here https://github.com/EntityGraphQL/EntityGraphQL/issues/314
            var schema = SchemaBuilder.FromObject<TestDataContext2>();

            var gql = new QueryRequest
            {
                Query = @"query Query($filter: String!) {
                    tasks(filter: $filter) { name }
                }",
                Variables = new QueryVariables { { "filter", "isActive == true)" } } // extra ) bracket
            };
            var tree = schema.ExecuteRequestWithContext(gql, (TestDataContext2)new TestDataContext2().FillWithTestData(), null, null);
            Assert.Null(tree.Errors);
        }

        private class TestDataContext2 : TestDataContext
        {
            [UseFilter]
            public override List<Person> People { get; set; } = new List<Person>();
            [UseFilter]
            public override IEnumerable<Task> Tasks { get; set; } = new List<Task>();
        }
    }
}