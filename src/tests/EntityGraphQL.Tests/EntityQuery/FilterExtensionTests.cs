using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class FilterExtensionTests
{
    [Fact]
    public void SupportEntityQuery()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().GetField("users", null).UseFilter();
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
        schemaProvider.Query().GetField("users", null).UseFilter();
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
        schemaProvider.Query().GetField("users", null).UseFilter();
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
        schemaProvider.Query().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    users(filter: $filter) { field2 }
                }",
            Variables = new QueryVariables { { "filter", "field2 == \"2\"" } },
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
        schemaProvider.Query().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String) {
                    users(filter: $filter) { field2 }
                }",
            // do not pass any values. p.filter.HasValue will be false (and work)
            Variables = [],
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
        schemaProvider.Query().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query() {
                    users { field2 }
                }",
            // do not pass any values. p.filter.HasValue will be false (and work)
            Variables = [],
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
            Variables = new QueryVariables { { "filter", "field2 == \"2\"" } },
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
            Variables = new QueryVariables { { "filter", "field2 == \"2\" or field2 == \"3\"" } },
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
            Variables = new QueryVariables { { "filter", "field2 == \"2\" and field2 == \"2\"" } },
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
            Variables = new QueryVariables { { "filter", "field2 != \"3\"" } },
        };
        var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(1, Enumerable.Count(users));
        var user = Enumerable.First(users);
        Assert.Equal("2", user.field2);
    }

    [Fact]
    public void SupportUseFilterWithIsAnyStatementInts()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query {
	users(filter: ""id.isAny([1,5])"") { id field2 }
}",
        };
        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Id = 1 });
        context.Users.Add(new User { Id = 5 });
        context.Users.Add(new User { Id = 10 });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(2, Enumerable.Count(users));
        var user = Enumerable.First(users);
        Assert.Equal(1, user.id);
    }

    [Fact]
    public void SupportUseFilterWithIsAnyStatementNullableInts()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().ReplaceField("users", (ctx) => ctx.Users, "Return filtered users").UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query {
	users(filter: ""relationId.isAny([1,5])"") { id field2 relationId }
}",
        };
        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Id = 1 });
        context.Users.Add(new User { Id = 5, RelationId = 5 });
        context.Users.Add(new User { Id = 10 });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(1, Enumerable.Count(users));
        var user = Enumerable.First(users);
        Assert.Equal(5, user.id);
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
            Variables = new QueryVariables { { "filter", "(id == 2) || (id == 4)" } },
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
    public void SupportUseFilterOnNonRootOrTest()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Project>().GetField("tasks", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    projects {
                        tasks(filter: $filter) { id name }
                    }
                }",
            Variables = new QueryVariables { { "filter", "(name == \"task 2\") || (name == \"task not there\")" } },
        };
        var tree = schema.ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);
        Assert.Null(tree.Errors);
        dynamic projects = ((IDictionary<string, object>)tree.Data!)["projects"];
        Assert.Equal(1, Enumerable.Count(projects));
        var project = Enumerable.First(projects);
        Assert.Single(project.tasks);
        Assert.Equal("task 2", Enumerable.ElementAt(project.tasks, 0).name);
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
            Variables = new QueryVariables { { "filter", "name == \"Luke\"" } },
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
            Variables = new QueryVariables
            {
                { "filter", "isActive == true)" },
            } // extra ) bracket
            ,
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
            Variables = new QueryVariables { { "filter", "projects.any(name == \"Home\")" } },
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
            Variables = new QueryVariables { { "filter", "projects.count() == 2" } },
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
            Variables = new QueryVariables { { "filter", "projects.count(name.startsWith(\"Plan\")) > 0" } },
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
            Variables = new QueryVariables { { "filter", "height > 170" } },
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
            Variables = new QueryVariables { { "filter", "name == \"Кирил\"" } },
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
            Variables = new QueryVariables { { "filter", "tasks.orderByDesc(hoursEstimated).first().hoursEstimated > 1" } },
        };
        var data = new TestDataContext
        {
            Projects =
            [
                new Project { Name = "Project 1", Tasks = [new Task { Id = 1, HoursEstimated = 0 }, new Task { Id = 2, HoursEstimated = 1 }] },
                new Project { Name = "Project 2", Tasks = [new Task { Id = 1, HoursEstimated = 0 }, new Task { Id = 2, HoursEstimated = 2 }] },
            ],
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
            Variables = new QueryVariables { { "filter", "gender == 'Male'" } },
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
    public void SupportUseFilterWithServiceAndNonServiceFields()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // Expose people with UseFilter
        schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people").UseFilter();
        // Add a service-backed field on Person
        schema.Type<Person>().AddField("age", "Person's age").Resolve<AgeService>((p, age) => age.GetAge(p.Birthday));

        // Mixed filter: non-service (lastName, name) and service field (age)
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(filter: ""lastName == \""Frank\"" and (age > 21 or name == \""Tom\"")"") {
                        id name lastName age
                    }
                }",
        };

        // Test data: only Jill should match after both passes
        var data = new TestDataContext();
        data.People.Add(
            new Person
            {
                Id = 1,
                Name = "Jill",
                LastName = "Frank",
                Birthday = DateTime.Now.AddYears(-22),
            }
        );
        data.People.Add(
            new Person
            {
                Id = 2,
                Name = "Cheryl",
                LastName = "Frank",
                Birthday = DateTime.Now.AddYears(-10),
            }
        );
        data.People.Add(
            new Person
            {
                Id = 3,
                Name = "Tom",
                LastName = "Smith",
                Birthday = DateTime.Now.AddYears(-30),
            }
        );

        // Provide the service for the service-backed field
        var services = new ServiceCollection();
        services.AddSingleton(new AgeService());

        var result = schema.ExecuteRequestWithContext(gql, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        dynamic people = ((IDictionary<string, object>)result.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Jill", person.name);
        Assert.Equal("Frank", person.lastName);
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
            Variables = new QueryVariables { { "filter", "birthday > \"2024-09-08T07:00:00.000Z\"" } },
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

    [Fact]
    public void SupportGraphQLVariablesInFilter()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"
                query GetUsersByField($fieldValue: String) {
                    users(filter: ""field2 == $fieldValue"") { 
                        field2 
                    }
                }",
            Variables = new QueryVariables { { "fieldValue", "2" } },
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
    public void SupportMultipleGraphQLVariablesInFilter()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"
                query GetUsersByFields($field1Value: Int, $field2Value: String) {
                    users(filter: ""field1 == $field1Value && field2 == $field2Value"") { 
                        field1
                        field2 
                    }
                }",
            Variables = new QueryVariables { { "field1Value", 2 }, { "field2Value", "2" } },
        };

        var context = new TestDataContext().FillWithTestData();
        context.Users.Add(new User { Field1 = 1, Field2 = "1" });
        context.Users.Add(new User { Field1 = 3, Field2 = "3" });
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic users = ((IDictionary<string, object>)tree.Data!)["users"];
        Assert.Equal(1, Enumerable.Count(users));
        var user = Enumerable.First(users);
        Assert.Equal(2, user.field1);
        Assert.Equal("2", user.field2);
    }

    [Fact]
    public void ThrowsErrorForUndefinedVariableInFilter()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().GetField("users", null).UseFilter();
        var gql = new QueryRequest
        {
            Query =
                @"
                query GetUsersByField {
                    users(filter: ""field2 == $undefinedVariable"") {
                        field2
                    }
                }",
            Variables = new QueryVariables { { "fieldValue", "2" } },
        };

        var context = new TestDataContext().FillWithTestData();
        var tree = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.NotNull(tree.Errors);

        Assert.Contains("Field 'users' - Variable $undefinedVariable not found in variables.", tree.Errors.First().Message);
    }

    [Fact]
    public void SupportUseFilterSelectManyMethod()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();
        var gql = new QueryRequest
        {
            Query =
                @"query Query($filter: String!) {
                    people(filter: $filter) { id name }
                }",
            Variables = new QueryVariables { { "filter", "projects.selectMany(tasks).any(name == \"Task 1\")" } },
        };
        var data = new TestDataContext2();
        var person1 = DataFiller.MakePerson(1, null, null);
        person1.Name = "Alice";
        person1.Projects.Add(new Project { Name = "Project A", Tasks = [new Task { Name = "Task 1" }, new Task { Name = "Task 2" }] });
        person1.Projects.Add(new Project { Name = "Project B", Tasks = [new Task { Name = "Task 3" }] });
        data.People.Add(person1);

        var person2 = DataFiller.MakePerson(2, null, null);
        person2.Name = "Bob";
        person2.Projects.Add(new Project { Name = "Project C", Tasks = [new Task { Name = "Task 4" }] });
        data.People.Add(person2);

        var tree = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(1, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Alice", person.name);
    }

    [Fact]
    public void SupportFilterWithNullableIntComparedToLiteral()
    {
        // Test for issue #484 - nullable int fields compared to numeric literals should not cause type misalignment
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().ReplaceField("users", (ctx) => ctx.Users, "Return filtered users").UseFilter();

        var context = new TestDataContext();
        context.Users.Add(new User { Id = 1, RelationId = 4726 });
        context.Users.Add(new User { Id = 2, RelationId = 1000 });
        context.Users.Add(new User { Id = 3, RelationId = null });
        context.Users.Add(new User { Id = 4, RelationId = 4726 });

        var gql = new QueryRequest
        {
            Query =
                @"query {
	users(filter: ""relationId == 4726"") { id relationId }
}",
        };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic users = ((IDictionary<string, object>)result.Data!)["users"];
        Assert.Equal(2, Enumerable.Count(users));

        var usersList = Enumerable.ToList(users);
        Assert.Equal(1, usersList[0].id);
        Assert.Equal(4726, usersList[0].relationId);
        Assert.Equal(4, usersList[1].id);
        Assert.Equal(4726, usersList[1].relationId);
    }

    [Fact]
    public void SupportFilterWithNullableIntComparedToLiteralReversed()
    {
        // Test for issue #484 - ensure reverse order (literal == field) also works correctly
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().ReplaceField("users", (ctx) => ctx.Users, "Return filtered users").UseFilter();

        var context = new TestDataContext();
        context.Users.Add(new User { Id = 1, RelationId = 4726 });
        context.Users.Add(new User { Id = 2, RelationId = 1000 });
        context.Users.Add(new User { Id = 3, RelationId = null });
        context.Users.Add(new User { Id = 4, RelationId = 4726 });

        var gql = new QueryRequest
        {
            Query =
                @"query {
	users(filter: ""4726 == relationId"") { id relationId }
}",
        };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic users = ((IDictionary<string, object>)result.Data!)["users"];
        Assert.Equal(2, Enumerable.Count(users));

        var usersList = Enumerable.ToList(users);
        Assert.Equal(1, usersList[0].id);
        Assert.Equal(4726, usersList[0].relationId);
        Assert.Equal(4, usersList[1].id);
        Assert.Equal(4726, usersList[1].relationId);
    }

    [Fact]
    public void SupportFilterWithNullableIntComparedToLiteralNotEqual()
    {
        // Additional test for issue #484 - testing != operator
        // Note: In nullable comparisons, != will match null values
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().ReplaceField("users", (ctx) => ctx.Users, "Return filtered users").UseFilter();

        var context = new TestDataContext();
        context.Users.Add(new User { Id = 1, RelationId = 4726 });
        context.Users.Add(new User { Id = 2, RelationId = 1000 });
        context.Users.Add(new User { Id = 3, RelationId = null });

        var gql = new QueryRequest
        {
            Query =
                @"query {
	users(filter: ""relationId != 4726"") { id relationId }
}",
        };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic users = ((IDictionary<string, object>)result.Data!)["users"];
        // Should match both the one with 1000 and the one with null (as null != 4726)
        Assert.Equal(2, Enumerable.Count(users));

        var usersList = Enumerable.ToList(users);
        Assert.Equal(2, usersList[0].id);
        Assert.Equal(1000, usersList[0].relationId);
        Assert.Equal(3, usersList[1].id);
        Assert.Null(usersList[1].relationId);
    }

    [Fact]
    public void SupportFilterWithNullableShortComparedToLiteral()
    {
        // Test for issue #484 - testing with short type to ensure it's handled
        var schemaProvider = SchemaBuilder.FromObject<TestContextWithNullableShort>();
        schemaProvider.Query().ReplaceField("items", (ctx) => ctx.Items, "Return filtered items").UseFilter();

        var context = new TestContextWithNullableShort();
        context.Items.Add(new ItemWithNullableShort { Id = 1, Count = 100 });
        context.Items.Add(new ItemWithNullableShort { Id = 2, Count = 200 });
        context.Items.Add(new ItemWithNullableShort { Id = 3, Count = null });

        var gql = new QueryRequest { Query = @"query { items(filter: ""count == 100"") { id count } }" };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic items = ((IDictionary<string, object>)result.Data!)["items"];
        Assert.Equal(1, Enumerable.Count(items));

        var item = Enumerable.First(items);
        Assert.Equal(1, item.id);
        Assert.Equal((short)100, item.count);
    }

    private class TestContextWithNullableShort
    {
        public List<ItemWithNullableShort> Items { get; set; } = [];
    }

    private class ItemWithNullableShort
    {
        public int Id { get; set; }
        public short? Count { get; set; }
    }

    [Fact]
    public void SupportFilterWithIntComparedToDouble()
    {
        // Test for integral to floating-point conversion
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.Query().ReplaceField("users", (ctx) => ctx.Users, "Return filtered users").UseFilter();

        var context = new TestDataContext();
        context.Users.Add(new User { Id = 1, Field1 = 100 });
        context.Users.Add(new User { Id = 2, Field1 = 200 });
        context.Users.Add(new User { Id = 3, Field1 = 150 });

        var gql = new QueryRequest { Query = @"query { users(filter: ""field1 > 150.5"") { id field1 } }" };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic users = ((IDictionary<string, object>)result.Data!)["users"];
        Assert.Equal(1, Enumerable.Count(users));

        var user = Enumerable.First(users);
        Assert.Equal(2, user.id);
        Assert.Equal(200, user.field1);
    }

    [Fact]
    public void SupportFilterWithDoubleFieldComparedToInt()
    {
        // Test for floating-point field with integral literal
        var schemaProvider = SchemaBuilder.FromObject<TestContextWithDouble>();
        schemaProvider.Query().ReplaceField("items", (ctx) => ctx.Items, "Return filtered items").UseFilter();

        var context = new TestContextWithDouble();
        context.Items.Add(new ItemWithDouble { Id = 1, Value = 100.5 });
        context.Items.Add(new ItemWithDouble { Id = 2, Value = 200.5 });
        context.Items.Add(new ItemWithDouble { Id = 3, Value = 50.5 });

        var gql = new QueryRequest { Query = @"query { items(filter: ""value > 100"") { id value } }" };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic items = ((IDictionary<string, object>)result.Data!)["items"];
        Assert.Equal(2, Enumerable.Count(items));
    }

    private class TestContextWithDouble
    {
        public List<ItemWithDouble> Items { get; set; } = [];
    }

    private class ItemWithDouble
    {
        public int Id { get; set; }
        public double Value { get; set; }
    }

    [Fact]
    public void SupportFilterWithFloatFieldComparedToDoubleLiteral()
    {
        // Test for float field with double literal (potential type mismatch)
        var schemaProvider = SchemaBuilder.FromObject<TestContextWithFloat>();
        schemaProvider.Query().ReplaceField("items", (ctx) => ctx.Items, "Return filtered items").UseFilter();

        var context = new TestContextWithFloat();
        context.Items.Add(new ItemWithFloat { Id = 1, Value = 100.5f });
        context.Items.Add(new ItemWithFloat { Id = 2, Value = 200.5f });
        context.Items.Add(new ItemWithFloat { Id = 3, Value = 50.5f });

        var gql = new QueryRequest { Query = @"query { items(filter: ""value > 100.5"") { id value } }" };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic items = ((IDictionary<string, object>)result.Data!)["items"];
        Assert.Equal(1, Enumerable.Count(items));
        var item = Enumerable.First(items);
        Assert.Equal(2, item.id);
    }

    private class TestContextWithFloat
    {
        public List<ItemWithFloat> Items { get; set; } = [];
    }

    private class ItemWithFloat
    {
        public int Id { get; set; }
        public float Value { get; set; }
    }

    [Fact]
    public void SupportFilterWithNullableFloatComparedToDoubleLiteral()
    {
        // Test for nullable float field with double literal
        var schemaProvider = SchemaBuilder.FromObject<TestContextWithNullableFloat>();
        schemaProvider.Query().ReplaceField("items", (ctx) => ctx.Items, "Return filtered items").UseFilter();

        var context = new TestContextWithNullableFloat();
        context.Items.Add(new ItemWithNullableFloat { Id = 1, Value = 100.5f });
        context.Items.Add(new ItemWithNullableFloat { Id = 2, Value = 200.5f });
        context.Items.Add(new ItemWithNullableFloat { Id = 3, Value = null });

        var gql = new QueryRequest { Query = @"query { items(filter: ""value == 100.5"") { id value } }" };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic items = ((IDictionary<string, object>)result.Data!)["items"];
        Assert.Equal(1, Enumerable.Count(items));
        var item = Enumerable.First(items);
        Assert.Equal(1, item.id);
    }

    private class TestContextWithNullableFloat
    {
        public List<ItemWithNullableFloat> Items { get; set; } = [];
    }

    private class ItemWithNullableFloat
    {
        public int Id { get; set; }
        public float? Value { get; set; }
    }

    [Fact]
    public void SupportFilterWithDecimalFieldComparedToDoubleLiteral()
    {
        // Test for decimal field with double literal
        var schemaProvider = SchemaBuilder.FromObject<TestContextWithDecimal>();
        schemaProvider.Query().ReplaceField("items", (ctx) => ctx.Items, "Return filtered items").UseFilter();

        var context = new TestContextWithDecimal();
        context.Items.Add(new ItemWithDecimal { Id = 1, Value = 100.5m });
        context.Items.Add(new ItemWithDecimal { Id = 2, Value = 200.5m });
        context.Items.Add(new ItemWithDecimal { Id = 3, Value = 50.5m });

        var gql = new QueryRequest { Query = @"query { items(filter: ""value > 100.5"") { id value } }" };

        var result = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic items = ((IDictionary<string, object>)result.Data!)["items"];
        Assert.Equal(1, Enumerable.Count(items));
        var item = Enumerable.First(items);
        Assert.Equal(2, item.id);
    }

    private class TestContextWithDecimal
    {
        public List<ItemWithDecimal> Items { get; set; } = [];
    }

    private class ItemWithDecimal
    {
        public int Id { get; set; }
        public decimal Value { get; set; }
    }

    private class TestDataContext2 : TestDataContext
    {
        [UseFilter]
        public override List<Person> People { get; set; } = [];

        [UseFilter]
        public override IEnumerable<Task> Tasks { get; set; } = new List<Task>();
    }
}
