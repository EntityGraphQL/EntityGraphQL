using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
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

    private class Person
    {
        public string Name { get; set; } = "";
        public Person? Boss { get; set; }
    }

    private class Employee : Person
    {
        public int Salary { get; set; }
    }

    private class Office
    {
        public List<Person> People { get; } =
            [
                new Person
                {
                    Name = "Jo",
                    Boss = new Employee { Name = "Sam", Salary = 10 },
                },
                new Person
                {
                    Name = "Al",
                    Boss = new Employee { Name = "Lee", Salary = 3 },
                },
            ];
    }

    [Fact]
    public void A_filter_through_a_field_declared_as_a_base_type_still_finds_the_derived_types_fields()
    {
        // boss is declared Person, its expression an Employee: the filter finds Employee by dotnet type, as before.
        var schema = new SchemaProvider<Office>();
        var person = schema.AddType<Person>("Person", null);
        person.AddField("name", p => p.Name, null);
        schema.AddType<Employee>("Employee", null).AddField("salary", e => e.Salary, null);
        person.AddField("boss", p => (Employee)p.Boss!, null).Returns("Person");
        schema.Query().AddField("people", o => o.People, null).UseFilter();

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people(filter: \"boss.salary > 5\") { name } }" }, new Office(), null, null);
        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal("Jo", (string)Enumerable.First(people).name);
    }

    private class Tagged
    {
        public Dictionary<string, string> Tags { get; set; } = new() { ["a"] = "b" };
    }

    private class TaggedContext
    {
        public List<Tagged> Items { get; } = [new()];
    }

    [Fact]
    public void A_dictionary_property_is_still_a_list_of_key_value_pairs()
    {
        // Why a dictionary is not excluded from being a list everywhere: this is a list on purpose.
        var schema = SchemaBuilder.FromObject<TaggedContext>();
        Assert.Contains("tags: [StringStringKeyValuePair", schema.ToGraphQLSchemaString());
    }

    private class IdAndID
    {
        public int Id { get; set; }
        public int ID { get; set; }
    }

    private class Holder
    {
        public List<IdAndID> Both { get; } = [];
    }

    [Fact]
    public void The_item_by_id_beside_a_list_of_a_type_with_two_matching_Id_members_still_builds()
    {
        // The item type is added by hand, so only item(id) reads its Id - and must pick one, not throw
        // (Expression.PropertyOrField matches case-insensitively, so it threw on Id and ID before).
        var schema = new SchemaProvider<Holder>();
        schema.AddType<IdAndID>("Both", null).AddField("id", b => b.Id, null);
        schema.Query().AddAllFields();

        Assert.Contains("bothById(id: Int!): Both", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void Returns_on_a_field_typed_object_takes_the_named_types_dotnet_type()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("someone", ctx => (object)ctx.People.First(), "Someone").Returns("Person");

        Assert.Equal(typeof(EntityGraphQL.Tests.Person), schema.Query().GetField("someone", null).ReturnType.TypeDotnet);
        var data = new TestDataContext();
        data.People.Add(new EntityGraphQL.Tests.Person { Id = 1, Name = "Jo" });
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ someone { name } }" }, data, null, null);
        Assert.Null(result.Errors);
    }

    private class Numbers
    {
        public List<int> Values { get; } = [1, 2, 3];
        public List<string> Emails { get; } = ["a@b.c"];
    }

    [Fact]
    public void Paging_a_scalar_named_type_changes_only_the_items()
    {
        // The items' schema type is for the items: totalItems is still an Int, a cursor still a String.
        var schema = SchemaBuilder.FromObject<Numbers>();
        schema.AddScalarType<int>("PositiveInt", "More than 0");
        schema.AddScalarType<string>("Email", "An email address");
        schema.Query().ReplaceField("values", n => n.Values, null).Returns("PositiveInt").UseOffsetPaging();
        schema.Query().ReplaceField("emails", n => n.Emails, null).Returns("Email").UseConnectionPaging();

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("items: [PositiveInt", sdl);
        Assert.Contains("totalItems: Int!", sdl);
        Assert.Contains("node: Email", sdl);
        Assert.Contains("cursor: String!", sdl);
    }
}
