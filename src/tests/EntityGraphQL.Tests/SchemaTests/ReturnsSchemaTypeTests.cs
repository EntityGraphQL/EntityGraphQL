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

    private class Staffed
    {
        public List<Person> People { get; } = [new Person { Name = "Jo" }];
        public IEnumerable<Employee> Staff { get; } = [new Employee { Name = "Sam", Salary = 10 }];
    }

    [Fact]
    public void An_aggregate_type_is_shared_by_a_field_over_a_derived_type()
    {
        // As on main: the aggregate's members take IEnumerable<Person>, which an IEnumerable<Employee> is.
        var schema = new SchemaProvider<Staffed>();
        schema.AddType<Person>("Person", null).AddField("name", p => p.Name, null);
        schema.Query().AddField("people", s => s.People, null).UseAggregate();
        schema.Query().AddField("staff", s => s.Staff, null).Returns("Person").UseAggregate();

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ staff { aggregate { count } } }" }, new Staffed(), null, null);
        Assert.Null(result.Errors);
    }

    private class Remapped
    {
        public int Id { get; set; }
        public string ExternalId { get; set; } = "";
    }

    private class RemappedContext
    {
        public List<Remapped> Things { get; } = [new Remapped { Id = 1, ExternalId = "ext-1" }];
    }

    [Fact]
    public void Item_by_id_compares_the_id_field_not_the_Id_member()
    {
        // id is ExternalId, a string: item(id) compares that, not the int Id member.
        var schema = new SchemaProvider<RemappedContext>();
        schema.AddType<Remapped>("Thing", null).AddField("id", t => t.ExternalId, null);
        schema.Query().AddAllFields();

        Assert.Contains("thing(id: String!): Thing", schema.ToGraphQLSchemaString());
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ thing(id: \"ext-1\") { id } }" }, new RemappedContext(), null, null);
        Assert.Null(result.Errors);
        dynamic thing = result.Data!["thing"]!;
        Assert.Equal("ext-1", (string)thing.id);
    }

    private class Argued
    {
        public int Id { get; set; }
    }

    private class ArguedContext
    {
        public List<Argued> Things { get; } = [new Argued { Id = 1 }, new Argued { Id = 2 }];
    }

    [Fact]
    public void Item_by_id_beside_an_id_field_with_arguments_still_works()
    {
        // Its expression has an arguments parameter item(id) would leave unbound: the Id member instead, as on main.
        var schema = new SchemaProvider<ArguedContext>();
        schema.AddType<Argued>("Thing", null).AddField("id", new { offset = 0 }, (t, args) => t.Id + args.offset, null);
        schema.Query().AddAllFields();

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ thing(id: 2) { id } }" }, new ArguedContext(), null, null);
        Assert.Null(result.Errors);
        dynamic thing = result.Data!["thing"]!;
        Assert.Equal(2, (int)thing.id);
    }

    private class Streamed
    {
        public async IAsyncEnumerable<EntityGraphQL.Tests.Person> PeopleAsync()
        {
            await System.Threading.Tasks.Task.Yield();
            yield return new EntityGraphQL.Tests.Person { Name = "Jo" };
        }
    }

    [Fact]
    public void Returns_over_an_async_stream_is_still_a_list()
    {
        // IAsyncEnumerable<T> is not awaited to one T, as a Task<T> is.
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("stream", "Stream").ResolveAsync<Streamed, EntityGraphQL.Tests.Person>((_, s) => s.PeopleAsync()).Returns("Person");
        Assert.True(schema.Query().GetField("stream", null).ReturnType.IsList);
    }

    private class Line
    {
        public string Sku { get; set; } = "";
    }

    private class Order
    {
        public List<Line> Items { get; set; } = [];
    }

    private class OrderPage
    {
        public List<Order> Items { get; set; } = [];
    }

    private class Shop
    {
        public List<OrderPage> Pages { get; } = [];
    }

    [Fact]
    public void A_known_member_schema_type_is_only_for_the_generated_types_own_member()
    {
        // An Order's own Items, built along the way, are its Lines - not the page's items' type.
        var schema = new SchemaProvider<Shop>();
        var orderSchemaType = schema.AddType<Order>("Order", null);
        schema.AddType<OrderPage>("OrderPage", null).AddAllFields(EntityGraphQL.Schema.FieldExtensions.GeneratedTypes.WithItems(typeof(OrderPage), "Items", orderSchemaType));
        // Order was added without fields: AddAllFields on it builds its own Items, from Line.
        orderSchemaType.AddAllFields(EntityGraphQL.Schema.FieldExtensions.GeneratedTypes.WithItems(typeof(OrderPage), "Items", orderSchemaType));

        Assert.Equal("Line", orderSchemaType.GetField("items", null).ReturnType.SchemaType.Name);
        Assert.Equal("Order", schema.Type("OrderPage").GetField("items", null).ReturnType.SchemaType.Name);
    }

    private interface IAnimal
    {
        string Name { get; }
    }

    private class Dog : IAnimal
    {
        public string Name { get; set; } = "";
        public int BarkVolume { get; set; }
    }

    private class Owner
    {
        public string Name { get; set; } = "";
        public IAnimal Pet { get; set; } = null!;
    }

    private class Kennel
    {
        public List<Owner> Owners { get; } =
            [
                new Owner
                {
                    Name = "Jo",
                    Pet = new Dog { Name = "Rex", BarkVolume = 9 },
                },
                new Owner
                {
                    Name = "Al",
                    Pet = new Dog { Name = "Pip", BarkVolume = 1 },
                },
            ];
    }

    [Fact]
    public void A_filter_through_a_field_declared_as_an_interface_finds_the_implementations_fields()
    {
        // pet is declared Animal (IAnimal), its expression a Dog, with no Implements: Dog's fields, as on main.
        var schema = new SchemaProvider<Kennel>();
        schema.AddType<IAnimal>("Animal", null).AddField("name", a => a.Name, null);
        schema.AddType<Dog>("Dog", null).AddField("barkVolume", d => d.BarkVolume, null);
        var owner = schema.AddType<Owner>("Owner", null);
        owner.AddField("name", o => o.Name, null);
        owner.AddField("pet", o => (Dog)o.Pet, null).Returns("Animal");
        schema.Query().AddField("owners", k => k.Owners, null).UseFilter();

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ owners(filter: \"pet.barkVolume > 3\") { name } }" }, new Kennel(), null, null);
        Assert.Null(result.Errors);
        dynamic owners = result.Data!["owners"]!;
        Assert.Equal("Jo", (string)Enumerable.First(owners).name);
    }

    private class PersonAggregate
    {
        public int Total { get; set; }
    }

    [Fact]
    public void An_aggregate_types_name_already_taken_by_another_type_fails_when_built()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<PersonAggregate>("PersonAggregate", "Not one the aggregate made");
        Assert.Throws<EntityGraphQLSchemaException>(() => schema.Query().ReplaceField("people", ctx => ctx.People, null).UseAggregate());
    }
}
