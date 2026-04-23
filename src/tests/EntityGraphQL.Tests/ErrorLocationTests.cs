using System.Collections.Generic;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.Directives;
using Xunit;

namespace EntityGraphQL.Tests;

public class ErrorLocationTests
{
    [Fact]
    public void ParseErrorsIncludeStructuredLocation()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var ex = Assert.Throws<EntityGraphQLException>(() => GraphQLParser.Parse("{ people {", schema));

        Assert.Equal(GraphQLErrorCategory.DocumentError, ex.Category);
        Assert.NotNull(ex.Location);
        Assert.Equal(10, ex.Location!.Position);
        Assert.Equal(1, ex.Location.Line);
        Assert.Equal(11, ex.Location.Column);
        Assert.Contains("line 1, column 11", ex.Message);
    }

    [Fact]
    public void RequestErrorsSerializeLocations()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people {" }, new TestDataContext(), null, null);

        var error = Assert.Single(result.Errors!);
        var location = Assert.Single(error.Locations!);
        Assert.Equal(10, location.Position);
        Assert.Equal(1, location.Line);
        Assert.Equal(11, location.Column);
    }

    [Fact]
    public void InvalidDirectiveLocationUsesSchemaException()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Query().AddField("name", ctx => "x", null).AddDirective(new InvalidFieldDirective()));

        Assert.Equal("InvalidFieldDirective not valid on FIELD_DEFINITION", ex.Message);
    }

    private sealed class InvalidFieldDirective : ISchemaDirective
    {
        public IEnumerable<TypeSystemDirectiveLocation> Location => [TypeSystemDirectiveLocation.QueryObject];

        public string ToGraphQLSchemaString()
        {
            return "@invalid on OBJECT";
        }
    }
}
