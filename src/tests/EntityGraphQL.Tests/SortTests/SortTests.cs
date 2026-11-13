using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests;

public class SortTests
{
    // a: lastName A, height 2 - b: lastName B, height 3 - c: lastName B, height 1
    private static TestDataContext MultiSortContext()
    {
        var context = new TestDataContext();
        context.People.Add(
            new Person
            {
                Id = 1,
                Name = "a",
                LastName = "A",
                Height = 2,
            }
        );
        context.People.Add(
            new Person
            {
                Id = 2,
                Name = "b",
                LastName = "B",
                Height = 3,
            }
        );
        context.People.Add(
            new Person
            {
                Id = 3,
                Name = "c",
                LastName = "B",
                Height = 1,
            }
        );
        return context;
    }

    private static string RunMultiSort(string sortArg, QueryVariables? variables = null)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort();
        var gql = new QueryRequest
        {
            Query = variables == null ? $"{{ people(sort: {sortArg}) {{ name }} }}" : "query($sort: [QueryPeopleSortInput!]) { people(sort: $sort) { name } }",
            Variables = variables,
        };
        var tree = schema.ExecuteRequestWithContext(gql, MultiSortContext(), null, null);
        Assert.Null(tree.Errors);
        var people = (IEnumerable<object>)((IDictionary<string, object>)tree.Data!)["people"];
        return string.Join(",", people.Select(p => (string)((dynamic)p).name));
    }

    [Theory]
    // every field set in one sort object is applied, in the order it is written
    [InlineData("{ lastName: DESC, height: ASC }", "c,b,a")]
    [InlineData("{ height: ASC, lastName: DESC }", "c,a,b")]
    [InlineData("[{ lastName: DESC, height: DESC }]", "b,c,a")]
    // and across list items
    [InlineData("[{ lastName: DESC }, { height: ASC }]", "c,b,a")]
    [InlineData("[{ lastName: ASC, height: DESC }, { name: DESC }]", "a,b,c")]
    public void SupportUseSortMultipleFieldsInOrderWritten(string sort, string expected)
    {
        Assert.Equal(expected, RunMultiSort(sort));
    }

    [Theory]
    [InlineData("{\"lastName\": \"DESC\", \"height\": \"ASC\"}", "c,b,a")]
    [InlineData("{\"height\": \"ASC\", \"lastName\": \"DESC\"}", "c,a,b")]
    [InlineData("[{\"height\": \"DESC\"}, {\"lastName\": \"ASC\"}]", "b,a,c")]
    public void SupportUseSortMultipleFieldsInOrderWrittenJsonVariable(string json, string expected)
    {
        var variables = new QueryVariables { { "sort", System.Text.Json.JsonDocument.Parse(json).RootElement } };
        Assert.Equal(expected, RunMultiSort("", variables));
    }

    [Fact]
    public void SupportUseSortMultipleFieldsInOrderWrittenObjectVariable()
    {
        // an object built in code sets its fields in the order it declares them - for an anonymous object, as written
        Assert.Equal("c,a,b", RunMultiSort("", new QueryVariables { { "sort", new { height = SortDirection.ASC, lastName = SortDirection.DESC } } }));
        Assert.Equal("c,b,a", RunMultiSort("", new QueryVariables { { "sort", new[] { new { lastName = SortDirection.DESC, height = SortDirection.ASC } } } }));
    }

    [Fact]
    public void SupportUseSortEmptySortObjectFirst()
    {
        // an empty sort object sorts nothing - the next one is still the first sort, not a ThenBy
        Assert.Equal("c,a,b", RunMultiSort("[{}, { height: ASC }]"));
    }

    [Fact]
    public void SupportUseSort()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort();
        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
            Variables = new QueryVariables { { "sort", new[] { new { lastName = SortDirection.DESC } } } },
        };
        var context = new TestDataContext().FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo" });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(2, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Zoo", person.lastName);
    }

    [Fact]
    public void SupportUseSortSingleObjectInline()
    {
        // GraphQL input coercion: a single value for a list argument is a list of one
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort();
        var gql = new QueryRequest { Query = @"{ people(sort: { lastName: DESC }) { lastName } }" };
        var context = new TestDataContext().FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo" });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal("Zoo", Enumerable.First(people).lastName);
    }

    [Fact]
    public void SupportUseSortSingleObjectVariable()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort();
        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
            Variables = new QueryVariables { { "sort", new { lastName = SortDirection.DESC } } },
        };
        var context = new TestDataContext().FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo" });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal("Zoo", Enumerable.First(people).lastName);
    }

    [Fact]
    public void SupportUseSortSingleObjectJsonVariable()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort();
        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
            Variables = new QueryVariables { { "sort", System.Text.Json.JsonDocument.Parse("{\"lastName\": \"DESC\"}").RootElement } },
        };
        var context = new TestDataContext().FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo" });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal("Zoo", Enumerable.First(people).lastName);
    }

    [Fact]
    public void TestSortAttribute()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext2>();

        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
            Variables = new QueryVariables { { "sort", new[] { new { lastName = "DESC" } } } },
        };
        TestDataContext2 context = new();
        context.FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo" });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(2, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Zoo", person.lastName);
        var inputType = schema.GetSchemaType("QueryPeopleSortInput", null);
        Assert.NotNull(inputType);
        var fields = inputType.GetFields().ToList();
        Assert.DoesNotContain("guid", fields.Select(f => f.Name));
    }

    private class TestDataContext2 : TestDataContext
    {
        [UseSort]
        public override List<Person> People { get; set; } = [];
    }

    [Fact]
    public void SupportUseSortSelectSortFields()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort((Person person) => new { person.Height, person.Name });
        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
            Variables = new QueryVariables { { "sort", new[] { new { height = "ASC" } } } },
        };
        var context = new TestDataContext().FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo", Height = 1 });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(2, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Zoo", person.lastName);
        var schemaType = schema.Type("QueryPeopleSortInput");
        var fields = schemaType.GetFields().ToList();
        Assert.Equal(3, fields.Count);
        Assert.Equal("__typename", fields[0].Name);
        Assert.Equal("height", fields[1].Name);
        Assert.Equal("name", fields[2].Name);
    }

    [Fact]
    public void SupportUseSortSelectSortFieldsDeepProperty()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort((Person person) => new { managerName = person.Manager!.Name, person.Name });
        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
            Variables = new QueryVariables { { "sort", new[] { new { managerName = "ASC" } } } },
        };
        var context = new TestDataContext().FillWithTestData();
        foreach (var p in context.People)
        {
            p.Manager = new Person { Name = "zbe" };
        }
        context.People.Add(
            new Person
            {
                LastName = "Zoo",
                Manager = new Person { Name = "Abe" },
            }
        );
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(2, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Zoo", person.lastName);
        var schemaType = schema.Type("QueryPeopleSortInput");
        var fields = schemaType.GetFields().ToList();
        Assert.Equal(3, fields.Count);
        Assert.Equal("__typename", fields[0].Name);
        Assert.Equal("managerName", fields[1].Name);
        Assert.Equal("name", fields[2].Name);
    }

    [Fact]
    public void SupportUseSortDefaultWithSelectSortFields()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort((Person person) => new { person.Height, person.Name }, (Person person) => person.LastName, SortDirection.DESC);
        var gql = new QueryRequest
        {
            Query =
                @"query {
                    people { lastName }
                }",
        };
        var context = new TestDataContext().FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo", Height = 1 });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(2, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Zoo", person.lastName);
        var schemaType = schema.Type("QueryPeopleSortInput");
        var fields = schemaType.GetFields().ToList();
        Assert.Equal(3, fields.Count);
        Assert.Equal("__typename", fields[0].Name);
        Assert.Equal("height", fields[1].Name);
        Assert.Equal("name", fields[2].Name);
    }

    [Fact]
    public void SupportUseSortDefault()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<TestDataContext>().GetField("people", null).UseSort((Person person) => person.Height, SortDirection.ASC);
        var gql = new QueryRequest
        {
            Query =
                @"query {
                    people { lastName }
                }",
        };
        var context = new TestDataContext().FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo", Height = 1 });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(2, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Zoo", person.lastName);
        var schemaType = schema.Type("QueryPeopleSortInput");
        var fields = schemaType.GetFields().ToList();
        Assert.Equal(12, fields.Count);
        Assert.Contains("people(sort: [QueryPeopleSortInput!] = [{ height: ASC }]): [Person!]", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void SupportUseSortDefaultMulti()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Type<TestDataContext>()
            .GetField("people", null)
            .UseSort(new Sort<Person>((person) => person.Height, SortDirection.ASC), new Sort<Person>((person) => person.LastName, SortDirection.ASC));
        var gql = new QueryRequest
        {
            Query =
                @"query {
                    people { lastName }
                }",
        };
        var context = new TestDataContext().FillWithTestData();
        context.People.Add(new Person { LastName = "Zoo", Height = 10 });
        context.People.Add(new Person { LastName = "Abe", Height = 10 });
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic people = ((IDictionary<string, object>)tree.Data!)["people"];
        Assert.Equal(3, Enumerable.Count(people));
        var person = Enumerable.First(people);
        Assert.Equal("Abe", person.lastName);
        var schemaType = schema.Type("QueryPeopleSortInput");
        var fields = schemaType.GetFields().ToList();
        Assert.Equal(12, fields.Count);
        Assert.Contains("people(sort: [QueryPeopleSortInput!] = [{ height: ASC }, { lastName: ASC }]): [Person!]", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void SupportUseSortOnNonRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Project>().GetField("tasks", null).UseSort();
        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [ProjectTasksSortInput]) {
                    projects {
                        tasks(sort: $sort) { id }
                    }
                }",
            Variables = new QueryVariables { { "sort", new[] { new { id = "DESC" } } } },
        };
        var context = new TestDataContext().FillWithTestData();
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic projects = ((IDictionary<string, object>)tree.Data!)["projects"];
        Assert.Equal(1, Enumerable.Count(projects));
        var project = Enumerable.First(projects);
        Assert.Equal(4, Enumerable.Count(project.tasks));
        Assert.Equal(4, Enumerable.ElementAt(project.tasks, 0).id);
        Assert.Equal(3, Enumerable.ElementAt(project.tasks, 1).id);
        Assert.Equal(2, Enumerable.ElementAt(project.tasks, 2).id);
        Assert.Equal(1, Enumerable.ElementAt(project.tasks, 3).id);
    }

    [Fact]
    public void SupportUseSortOnNonRootVariableWithClass()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Project>().GetField("tasks", null).UseSort();
        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [ProjectTasksSortInput]) {
                    projects {
                        tasks(sort: $sort) { id }
                    }
                }",
            Variables = new QueryVariables
            {
                {
                    "sort",
                    new List<IdSort> { new IdSort { Id = SortDirection.DESC } }
                },
            },
        };
        var context = new TestDataContext().FillWithTestData();
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic projects = ((IDictionary<string, object>)tree.Data!)["projects"];
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
        schema.Type<Project>().GetField("tasks", null).UseSort();
        var gql = new QueryRequest
        {
            Query =
                @"query($sort: [ProjectTasksSortInput]) {
                    project(id: 55) {
                        tasks(sort: $sort) { id }
                    }
                }",
            Variables = new QueryVariables { { "sort", new[] { new { id = "DESC" } } } },
        };
        var context = new TestDataContext().FillWithTestData();
        var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(tree.Errors);
        dynamic project = ((IDictionary<string, object>)tree.Data!)["project"];
        Assert.Equal(4, Enumerable.Count(project.tasks));
        Assert.Equal(4, Enumerable.ElementAt(project.tasks, 0).id);
        Assert.Equal(3, Enumerable.ElementAt(project.tasks, 1).id);
        Assert.Equal(2, Enumerable.ElementAt(project.tasks, 2).id);
        Assert.Equal(1, Enumerable.ElementAt(project.tasks, 3).id);
    }

    [Fact]
    public void TestSortCustomField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        data.FillWithTestData();

        schema.UpdateType<Project>(type =>
        {
            type.AddField("maxHoursEstimatedForSingleTask", project => project.Tasks.Max(t => t.HoursEstimated), "Get the max hours estimated for a single task");
        });
        schema.Query().GetField("projects", null).UseSort(true);
        var gql = new QueryRequest
        {
            Query =
                @"query {
                    projects(sort: [{maxHoursEstimatedForSingleTask: ASC}]) {
                        maxHoursEstimatedForSingleTask
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null);
        Assert.Null(result.Errors);

        dynamic projects = result.Data!["projects"]!;
    }
}

internal class IdSort
{
    public SortDirection Id { get; set; }
}

internal class TestArgs
{
    public SortDirection? lastName { get; set; }
}
