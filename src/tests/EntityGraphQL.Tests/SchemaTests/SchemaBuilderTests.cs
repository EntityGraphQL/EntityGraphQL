using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
public class SchemaBuilderTests
{
    [Fact]
    public void TestMissingTypeError()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();
        schema.Query().AddField("people", ctx => ctx.People, "People");
        var gql = new QueryRequest
        {
            Query =
                @"{
                people {
                    name
                }
            }",
        };
        var context = new TestDataContext().FillWithTestData();
        // we have not added Person type before trying to execute
        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.NotNull(res.Errors);
        Assert.Equal("No schema type found for dotnet type 'Person'. Make sure you add it or add a type mapping. Lookup failed for field 'people' on type 'Query'", res.Errors[0].Message);
    }

    [Fact]
    public void TestMissingTypeErrorNonRoot()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();
        schema.Query().AddField("people", ctx => ctx.People, "People");
        schema.AddType<Person>("A person").AddField("tasks", p => p.Tasks, "Tasks");
        var gql = new QueryRequest
        {
            Query =
                @"{
                people {
                    tasks { name }
                }
            }",
        };
        var context = new TestDataContext().FillWithTestData();
        context.People[0].Tasks = [];
        // we have not added Task type before trying to execute
        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.NotNull(res.Errors);
        Assert.Equal("No schema type found for dotnet type 'Task'. Make sure you add it or add a type mapping. Lookup failed for field 'tasks' on type 'Person'", res.Errors[0].Message);
    }
}
