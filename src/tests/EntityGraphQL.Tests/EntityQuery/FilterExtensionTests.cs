using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;
using static EntityGraphQL.Schema.ArgumentHelper;

namespace EntityGraphQL.Tests;

public class FilterExtensionTests
{
    [Fact]
    public void SupportEntityQuery()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
        var gql = new QueryRequest
        {
            Query =
                @"query {
	users(filter: ""field2 == \""2\"" "") { field2 }
}",
        };
        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Field2 = "99" });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
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
            Query =
                @"query {
	users(filter: """") { field2 }
}",
        };
        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Field2 = "99" });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(2, Enumerable.Count(users));
    }

    [Fact]
    public void SupportEntityQueryStringWhitespace()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
        var gql = new QueryRequest
        {
            Query =
                @"query {
	users(filter: ""  "") { field2 }
}",
        };
        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Field2 = "99" });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(2, Enumerable.Count(users));
    }

    [Fact]
    public void SupportEntityQueryArgument()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().ReplaceField("users", new { filter = EntityQuery<User>() }, (ctx, p) => ctx.Users.WhereWhen(p.filter, p.filter.HasValue), "Return filtered users");
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
            Variables = new QueryVariables { { "filter", "field2 == \"2\"" } }
        };
        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Field2 = "99" });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
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
            Query =
                @"query Query($filter: String) {
                    users(filter: $filter) { field2 }
                }",
            // do not pass any values. p.filter.HasValue will be false (and work)
            Variables = new QueryVariables()
        };
        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Field2 = "99" });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
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
            Query =
                @"query Query() {
                    users { field2 }
                }",
            // do not pass any values. p.filter.HasValue will be false (and work)
            Variables = new QueryVariables()
        };
        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Field2 = "99" });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
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
        schema.Type<TestDataContext>().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
            Variables = new QueryVariables { { "filter", "field2 == \"2\"" } }
        };
        var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(1, Enumerable.Count(users));
        var user = Enumerable.First(users);
        Assert.Equal("2", user.field2);
    }

    [Fact]
    public void SupportUseFilterWithOrStatement()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
        schema.Type<TestDataContext>().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
            Variables = new QueryVariables { { "filter", "field2 == \"2\" or field2 == \"3\"" } }
        };
        var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(1, Enumerable.Count(users));
        var user = Enumerable.First(users);
        Assert.Equal("2", user.field2);
    }

    [Fact]
    public void SupportUseFilterWithAndStatement()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
        schema.Type<TestDataContext>().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
            Variables = new QueryVariables { { "filter", "field2 == \"2\" and field2 == \"2\"" } }
        };
        var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(1, Enumerable.Count(users));
        var user = Enumerable.First(users);
        Assert.Equal("2", user.field2);
    }

    [Fact]
    public void SupportUseFilterWithnotEqualStatement()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
        schema.Type<TestDataContext>().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
            Variables = new QueryVariables { { "filter", "field2 != \"3\"" } }
        };
        var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(1, Enumerable.Count(users));
        var user = Enumerable.First(users);
        Assert.Equal("2", user.field2);
    }

    [Fact]
    public void SupportUseFilterOnNonRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Project>().GetField("tasks", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    projects {
                        tasks(filter: $filter) { id }
                    }
                }",
            Variables = new QueryVariables { { "filter", "(id == 2) || (id == 4)" } }
        };
        var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
        dynamic projects = ((IDictionary<string, object>)tree.Data!)["projects"];
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
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { name }
                }",
            Variables = new QueryVariables { { "filter", "name == \"Luke\"" } }
        };
        var tree = schema.ExecuteRequestWithContext(gql, (TestDataContext2)new TestDataContext2().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
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
            Query =
                @"query Query($filter: String!) {
                    tasks(filter: $filter) { name }
                }",
            Variables = new QueryVariables { { "filter", "isActive == true)" } } // extra ) bracket
        };
        var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext2().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
    }

    [Fact]
    public void SupportUseFilterAnyMethod()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { id name }
                }",
            Variables = new QueryVariables { { "filter", "projects.any(name == \"Home\")" } }
        };
        var data = new TestDataContext2().FillWithTestData();
        data.People.First().Name = "Lisa";
        data.People.First().Projects.Add(new Project { Name = "Home" });
        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Lisa", person.name);
    }

    [Fact]
    public void SupportUseFilterCountMethod()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { id name }
                }",
            Variables = new QueryVariables { { "filter", "projects.count() == 2" } }
        };
        var data = new TestDataContext2().FillWithTestData();
        data.People.First().Name = "Lisa";
        data.People.First().Projects.Add(new Project { Name = "Home" });
        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Lisa", person.name);
    }

    [Fact]
    public void SupportUseFilterCountMethodWithFilter()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { id name }
                }",
            Variables = new QueryVariables { { "filter", "projects.count(name.startsWith(\"Plan\")) > 0" } }
        };
        var data = new TestDataContext2().FillWithTestData();
        data.People.Add(DataFiller.MakePerson(33, null, null));
        data.People.Last().Name = "Bob";
        data.People.Last().Projects.Add(new Project { Name = "Plane" });
        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Bob", person.name);
    }

    [Fact]
    public void SupportUseFilterDecimalWithInt()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { id name height }
                }",
            Variables = new QueryVariables { { "filter", "height > 170" } }
        };
        var data = new TestDataContext2();
        var person1 = DataFiller.MakePerson(33, null, null);
        person1.Height = 180;
        data.People.Add(person1);
        var person2 = DataFiller.MakePerson(34, null, null);
        person2.Height = 160;
        data.People.Add(person2);
        Assert.Equal(2, data.People.Count);
        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal(180, person.height);
    }

    [Fact]
    public void SupportUseFilterUnicode()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { id name height }
                }",
            Variables = new QueryVariables { { "filter", "name == \"Кирил\"" } }
        };
        var data = new TestDataContext2();
        var person1 = DataFiller.MakePerson(33, null, null);
        person1.Name = "Bill";
        data.People.Add(person1);
        var person2 = DataFiller.MakePerson(34, null, null);
        person2.Name = "Кирил";
        data.People.Add(person2);
        Assert.Equal(2, data.People.Count);
        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Кирил", person.name);
    }

    [Fact]
    public void TestFilterMethodsChained()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().GetField("projects", null).UseFilter();
        schema.UpdateType<Project>(type =>
        {
            type.AddField("maxHoursEstimatedForSingleTask", project => project.Tasks.Max(t => t.HoursEstimated), "Get the max hours estimated for a single task");
        });
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    projects(filter: $filter) {
                        name
                        tasks { id }
                    }
                }",
            Variables = new QueryVariables { { "filter", "tasks.orderByDesc(hoursEstimated).first().hoursEstimated > 1" } }
        };
        var data = new TestDataContext
        {
            Projects =
            [
                new Project { Name = "Project 1", Tasks = [new Task { Id = 1, HoursEstimated = 0 }, new Task { Id = 2, HoursEstimated = 1 }] },
                new Project { Name = "Project 2", Tasks = [new Task { Id = 1, HoursEstimated = 0 }, new Task { Id = 2, HoursEstimated = 2 }] }
            ]
        };

        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);

        Assert.Null(tree.Errors);
        dynamic projects = ((IDictionary<string, object>)tree.Data!)["projects"];
        Assert.Equal(1, Enumerable.Count(projects));
        var project = Enumerable.First(projects);
        Assert.Equal(2, Enumerable.Count(project.tasks));
        Assert.Equal(1, Enumerable.ElementAt(project.tasks, 0).id);
        Assert.Equal(2, Enumerable.ElementAt(project.tasks, 1).id);
    }

    [Fact]
    public void SupportUseFilterEnumWithString()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { id name gender }
                }",
            Variables = new QueryVariables { { "filter", "gender == 'Male'" } }
        };
        var data = new TestDataContext2();
        var person1 = DataFiller.MakePerson(33, null, null);
        person1.Gender = Gender.Female;
        data.People.Add(person1);
        var person2 = DataFiller.MakePerson(34, null, null);
        person2.Gender = Gender.Male;
        data.People.Add(person2);
        Assert.Equal(2, data.People.Count);
        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal(Gender.Male, person.gender);
    }

    [Fact]
    public void SupportNullableDateTime()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { id name gender }
                }",
            Variables = new QueryVariables { { "filter", "birthday > \"2024-09-08T07:00:00.000Z\"" } }
        };
        var data = new TestDataContext2();
        var person1 = DataFiller.MakePerson(33, null, null);
        person1.Birthday = new DateTime(2024, 9, 9);
        data.People.Add(person1);
        var person2 = DataFiller.MakePerson(34, null, null);
        person2.Birthday = new DateTime(2024, 9, 7);
        data.People.Add(person2);
        Assert.Equal(2, data.People.Count);
        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal(33, person.id);
    }

    private class TestDataContext2 : TestDataContext
    {
        [UseFilter]
        public override List<Person> People { get; set; } = new List<Person>();

        [UseFilter]
        public override IEnumerable<Task> Tasks { get; set; } = new List<Task>();
    }
}
