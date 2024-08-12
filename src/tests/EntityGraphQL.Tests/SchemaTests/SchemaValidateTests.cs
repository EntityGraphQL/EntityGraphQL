using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
public class SchemaValidateTests
{
    [Fact]
    public void TestMissingTypeError()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();
        schema.Query().AddField("people", ctx => ctx.People, "People");
        var ex = Assert.Throws<EntityGraphQLCompilerException>(() => schema.Validate());
        Assert.Equal("Field 'people' on type 'Query' returns type 'EntityGraphQL.Tests.Person' that is not in the schema", ex.Message);
    }

    [Fact]
    public void TestMissingTypeErrorNonRoot()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();
        schema.Query().AddField("people", ctx => ctx.People, "People");
        schema.AddType<Person>("A person").AddField("tasks", p => p.Tasks, "Tasks");
        var ex = Assert.Throws<EntityGraphQLCompilerException>(() => schema.Validate());
        Assert.Equal("Field 'tasks' on type 'Person' returns type 'EntityGraphQL.Tests.Task' that is not in the schema", ex.Message);
    }
}
