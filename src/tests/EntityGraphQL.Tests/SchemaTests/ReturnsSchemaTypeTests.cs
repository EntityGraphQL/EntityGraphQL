using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests.SchemaTests;

/// <summary>Field.Returns("Type") on fields that are not property bags: what it must leave as it was.</summary>
public class ReturnsSchemaTypeTests
{
    private class Thing(string name)
    {
        public string Name { get; } = name;
    }

    private class ThingService
    {
        public System.Threading.Tasks.Task<List<Thing>> GetThingsAsync() => System.Threading.Tasks.Task.FromResult(new List<Thing> { new("a"), new("b") });
    }

    [Fact]
    public void An_async_field_keeps_its_list_type()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Thing>("Thing", null).AddAllFields();
        schema.Query().AddField("things", "Things").ResolveAsync<ThingService>((_, service) => service.GetThingsAsync()).Returns("Thing");

        var services = new ServiceCollection();
        services.AddSingleton<ThingService>();
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { name } }" }, new TestDataContext(), services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        dynamic things = result.Data!["things"]!;
        Assert.Equal(2, Enumerable.Count(things));
        Assert.True(schema.Query().GetField("things", null).ReturnType.IsList);
    }

    [Fact]
    public void Nullability_comes_from_the_named_type_as_before()
    {
        // A list of a value type named as Int was [Int!]! before Returns kept the field's own type.
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("numbers", _ => new List<int> { 1, 2 }, "Numbers").Returns("Int");

        Assert.Contains("numbers: [Int!]!", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void A_list_named_as_a_supertype_of_its_items_is_still_a_list()
    {
        // List<object> is itself an object, but its items are what the schema type names.
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<object>("Any", "Anything");
        schema.Query().AddField("anything", _ => new List<object> { 1, "a" }, "Anything").Returns("Any");

        Assert.True(schema.Query().GetField("anything", null).ReturnType.IsList);
    }
}
