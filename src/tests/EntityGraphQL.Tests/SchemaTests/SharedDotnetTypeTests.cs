using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.SchemaTests;

/// <summary>
/// Schema types that share one dotnet type - e.g. tables known only at runtime, each a property bag
/// (Dictionary&lt;string, object&gt;, as EF's shared-type entities are). The dotnet type cannot say which
/// schema type a value is, so a field names it with Returns("Type") and the schema has to take it
/// from the field: in selections, filters and sorts alike.
/// </summary>
public class SharedDotnetTypeTests
{
    private static Dictionary<string, object> Author(int id, string name) =>
        new()
        {
            ["id"] = id,
            ["name"] = name,
            ["priorities"] = new List<Dictionary<string, object>>(),
        };

    private static Dictionary<string, object> Priority(int id, string mergeRequest, int score, Dictionary<string, object> author)
    {
        var priority = new Dictionary<string, object>
        {
            ["id"] = id,
            ["mergeRequest"] = mergeRequest,
            ["score"] = score,
            ["author"] = author,
        };
        ((List<Dictionary<string, object>>)author["priorities"]).Add(priority);
        return priority;
    }

    private class BagContext
    {
        public List<Dictionary<string, object>> Authors { get; } = [];
        public List<Dictionary<string, object>> Priorities { get; } = [];
    }

    private static (SchemaProvider<BagContext> Schema, BagContext Data) Build(bool paged = false, bool idlessTypeFirst = false)
    {
        var data = new BagContext();
        var ada = Author(1, "Ada");
        var ben = Author(2, "Ben");
        data.Authors.AddRange([ada, ben]);
        data.Priorities.AddRange([Priority(1, "!12", 9, ada), Priority(2, "!13", 3, ben), Priority(3, "!14", 7, ada)]);

        var schema = new SchemaProvider<BagContext>();
        if (idlessTypeFirst)
            schema.AddType<Dictionary<string, object>>("Tag", null).AddField("label", t => (string)t["label"], null);
        // Authors first: the first schema type registered for the dotnet type is what a lookup by
        // dotnet type finds, so anything resolving Priority that way gets Author.
        var author = schema.AddType<Dictionary<string, object>>("Author", null);
        var priority = schema.AddType<Dictionary<string, object>>("Priority", null);

        author.AddField("id", a => (int)a["id"], null);
        author.AddField("name", a => (string)a["name"], null);
        var authorPriorities = author.AddField("priorities", a => (List<Dictionary<string, object>>)a["priorities"], null).Returns("Priority");
        if (paged)
            authorPriorities.UseOffsetPaging();

        priority.AddField("id", p => (int)p["id"], null);
        priority.AddField("mergeRequest", p => (string)p["mergeRequest"], null);
        priority.AddField("score", p => (int)p["score"], null);
        priority.AddField("author", p => (Dictionary<string, object>)p["author"], null).Returns("Author");

        schema.Query().AddField("authors", c => c.Authors, null).Returns("Author").UseFilter().UseSort(true);
        var priorities = schema.Query().AddField("priorities", c => c.Priorities, null).Returns("Priority").UseFilter().UseSort(true);
        if (paged)
            priorities.UseOffsetPaging().UseAggregate(AggregatePlacement.PagingWrapper);
        return (schema, data);
    }

    private static List<object?> Run(string query, string field, bool paged = false)
    {
        var (schema, data) = Build(paged);
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, data, null, null);
        Assert.Null(result.Errors);
        var value = ((IDictionary<string, object?>)result.Data!)[field]!;
        return value is IEnumerable<object?> list ? list.ToList() : [value];
    }

    private static object? Get(object? item, string name) => item!.GetType().GetField(name)!.GetValue(item);

    [Fact]
    public void A_field_returning_the_named_type_is_an_object_not_a_list()
    {
        // A dictionary is enumerable, which made a field returning one a list of its key/value pairs.
        var priorities = Run("{ priorities { mergeRequest author { name } } }", "priorities");
        Assert.Equal("Ada", Get(Get(priorities[0], "author"), "name"));
    }

    [Fact]
    public void A_field_returning_a_list_of_the_named_type_is_a_list()
    {
        var authors = Run("{ authors { name priorities { mergeRequest } } }", "authors");
        Assert.Equal(["!12", "!14"], ((IEnumerable<object?>)Get(authors[0], "priorities")!).Select(p => Get(p, "mergeRequest")));
    }

    [Fact]
    public void A_filter_uses_the_fields_schema_type()
    {
        var priorities = Run("{ priorities(filter: \"score > 5\") { mergeRequest } }", "priorities");
        Assert.Equal(["!12", "!14"], priorities.Select(p => Get(p, "mergeRequest")));
    }

    [Fact]
    public void A_filter_follows_a_reference_to_its_schema_type()
    {
        var priorities = Run("{ priorities(filter: \"author.name == \\\"Ben\\\"\") { mergeRequest } }", "priorities");
        Assert.Equal(["!13"], priorities.Select(p => Get(p, "mergeRequest")));
    }

    [Fact]
    public void A_filter_method_on_a_collection_uses_its_elements_schema_type()
    {
        var authors = Run("{ authors(filter: \"priorities.any(score > 8)\") { name } }", "authors");
        Assert.Equal(["Ada"], authors.Select(a => Get(a, "name")));
    }

    [Fact]
    public void A_sort_uses_the_fields_schema_type()
    {
        var priorities = Run("{ priorities(sort: [{ score: DESC }]) { mergeRequest } }", "priorities");
        Assert.Equal(["!12", "!14", "!13"], priorities.Select(p => Get(p, "mergeRequest")));
    }

    [Fact]
    public void A_filter_on_a_paged_field_uses_the_items_schema_type()
    {
        // Paging replaces the field's schema type with its Page wrapper; the filter is still over Priority.
        var page = Run("{ priorities(filter: \"score > 5\") { items { mergeRequest } } }", "priorities", paged: true);
        Assert.Equal(["!12", "!14"], ((IEnumerable<object?>)Get(page[0], "items")!).Select(p => Get(p, "mergeRequest")));
    }

    [Fact]
    public void A_filter_through_a_paged_field_uses_its_items_schema_type()
    {
        var authors = Run("{ authors(filter: \"priorities.items.any(score > 8)\") { name } }", "authors", paged: true);
        Assert.Equal(["Ada"], authors.Select(a => Get(a, "name")));
    }

    [Fact]
    public void Chained_collection_methods_keep_the_elements_schema_type()
    {
        var authors = Run("{ authors(filter: \"priorities.where(score > 5).any(score > 8)\") { name } }", "authors");
        Assert.Equal(["Ada"], authors.Select(a => Get(a, "name")));
    }

    [Fact]
    public void A_single_item_from_a_collection_keeps_its_schema_type()
    {
        var authors = Run("{ authors(filter: \"priorities.first().score == 9\") { name } }", "authors");
        Assert.Equal(["Ada"], authors.Select(a => Get(a, "name")));
    }

    [Fact]
    public void An_aggregate_on_a_paging_wrapper_uses_the_items_schema_type()
    {
        var page = Run("{ priorities { aggregate { count max { score } } } }", "priorities", paged: true);
        var aggregate = Get(page[0], "aggregate");
        Assert.Equal(3, Get(aggregate, "count"));
        Assert.Equal(9, Get(Get(aggregate, "max"), "score"));
    }

    [Fact]
    public void Two_tables_sharing_a_dotnet_type_each_get_their_own_paging_types()
    {
        // OffsetPage<T>, ConnectionEdge<T> and Connection<T> are one dotnet type for every property bag.
        var (schema, data) = Build();
        schema.Query().ReplaceField("authors", c => c.Authors, null).Returns("Author").UseConnectionPaging();
        schema.Query().ReplaceField("priorities", c => c.Priorities, null).Returns("Priority").UseOffsetPaging();

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("items: [Priority]!", sdl);
        Assert.Contains("item(id: Int!): Priority", sdl);
        Assert.Contains("node: Author!", sdl);
        // and no key/value pair scalar made of a property bag along the way
        Assert.DoesNotContain("KeyValuePair", sdl);

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ authors(first: 1) { edges { node { name } } } priorities(take: 2) { items { mergeRequest } } }" },
            data,
            null,
            null
        );
        Assert.Null(result.Errors);
        dynamic authors = ((IDictionary<string, object?>)result.Data!)["authors"]!;
        dynamic priorities = ((IDictionary<string, object?>)result.Data!)["priorities"]!;
        Assert.Equal("Ada", (string)Enumerable.First(authors.edges).node.name);
        Assert.Equal(2, Enumerable.Count(priorities.items));
    }

    [Fact]
    public void Each_shared_types_page_has_its_own_item_by_id()
    {
        // Built with the items' schema type, item(id) takes that type's id field, is one item, and says so.
        var (schema, data) = Build();
        schema.Query().ReplaceField("authors", c => c.Authors, null).Returns("Author").UseOffsetPaging();
        schema.Query().ReplaceField("priorities", c => c.Priorities, null).Returns("Priority").UseOffsetPaging();

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("\"\"\"Return a Author by its Id\"\"\"\n\titem(id: Int!): Author\n", sdl);
        Assert.Contains("\"\"\"Return a Priority by its Id\"\"\"\n\titem(id: Int!): Priority\n", sdl);

        // The query compiles over Priority's id field. (What it returns is not asserted: item(id) on a
        // page returns null for any type today, property bag or not.)
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ priorities { items { id } item(id: 3) { mergeRequest } } }" }, data, null, null);
        Assert.Null(result.Errors);
    }

    [Fact]
    public void A_shared_type_registered_after_one_without_an_id_still_gets_item_by_id()
    {
        // Tag is registered first, with no id field: a lookup by dotnet type finds it.
        var (schema, _) = Build(idlessTypeFirst: true);
        schema.Query().ReplaceField("priorities", c => c.Priorities, null).Returns("Priority").UseOffsetPaging();

        Assert.Contains("item(id: Int!): Priority", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void An_untyped_id_gets_no_item_by_id()
    {
        // a["id"] with no cast is an object: no argument type to give item(id), and Equal would compare references.
        var schema = new SchemaProvider<BagContext>();
        schema.AddScalarType<object>("Any", "Anything");
        var priority = schema.AddType<Dictionary<string, object>>("Priority", null);
        priority.AddField("id", p => p["id"], null);
        schema.Query().AddField("priorities", c => c.Priorities, null).Returns("Priority").UseOffsetPaging();

        Assert.DoesNotContain("item(id", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void A_filter_before_Returns_still_uses_the_named_type()
    {
        var (schema, data) = Build();
        var field = schema.Query().ReplaceField("priorities", c => c.Priorities, null);
        field.UseFilter();
        field.Returns("Priority");

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ priorities(filter: \"score > 5\") { mergeRequest } }" }, data, null, null);
        Assert.Null(result.Errors);
        Assert.Equal(2, ((IEnumerable<object?>)((IDictionary<string, object?>)result.Data!)["priorities"]!).Count());
    }

    [Fact]
    public void A_schema_type_of_the_page_name_that_is_not_a_page_fails_when_built()
    {
        var (schema, _) = Build();
        schema.AddType<Thing>("PriorityPage", "Not a page");
        Assert.Throws<EntityGraphQLSchemaException>(() => schema.Query().ReplaceField("priorities", c => c.Priorities, null).Returns("Priority").UseOffsetPaging());
    }

    private class Thing
    {
        public string Name { get; set; } = "";
    }

    private class IdService
    {
        public int IdOf(Dictionary<string, object> bag) => (int)bag["id"];
    }

    [Fact]
    public void An_id_from_a_service_gets_no_item_by_id()
    {
        // Its expression has the service's parameter too, which item(id) would leave unbound.
        var schema = new SchemaProvider<BagContext>();
        var priority = schema.AddType<Dictionary<string, object>>("Priority", null);
        priority.AddField("id", "Id").Resolve<IdService>((p, service) => service.IdOf(p));
        schema.Query().AddField("priorities", c => c.Priorities, null).Returns("Priority").UseOffsetPaging();

        Assert.DoesNotContain("item(id", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void A_filter_through_an_aggregated_field_uses_its_items_schema_type()
    {
        var (schema, data) = Build();
        ((SchemaType<Dictionary<string, object>>)schema.Type("Author")).ReplaceField("priorities", a => (List<Dictionary<string, object>>)a["priorities"], null).Returns("Priority").UseAggregate();
        // In a filter the aggregated field is its collection, as the wrapper is only the schema's.
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ authors(filter: \"priorities.any(score > 8)\") { name } }" }, data, null, null);
        Assert.Null(result.Errors);
        Assert.Single((IEnumerable<object?>)((IDictionary<string, object?>)result.Data!)["authors"]!);
    }

    private class Bags : List<Dictionary<string, object>>;

    [Theory]
    [InlineData(typeof(List<Dictionary<string, object>>), typeof(Dictionary<string, object>))]
    [InlineData(typeof(Bags), typeof(Dictionary<string, object>))] // a collection that is not generic itself
    [InlineData(typeof(Dictionary<string, object>), typeof(KeyValuePair<string, object>))] // a dictionary is of its pairs
    [InlineData(typeof(System.Threading.Tasks.Task<Dictionary<string, object>>), null)] // not a collection
    [InlineData(typeof(OffsetPage<Dictionary<string, object>>), null)] // nor is a page
    [InlineData(typeof(string), null)]
    public void What_a_collection_is_of(Type type, Type? elementType) => Assert.Equal(elementType, type.GetCollectionElementType());

    [Theory]
    [InlineData(typeof(Dictionary<string, object>), typeof(Dictionary<string, object>), false)] // a bag named as itself
    [InlineData(typeof(IDictionary<string, object>), typeof(Dictionary<string, object>), false)] // behind its interface
    [InlineData(typeof(List<Dictionary<string, object>>), typeof(Dictionary<string, object>), true)]
    [InlineData(typeof(IEnumerable<object>), typeof(Dictionary<string, object>), true)] // items less specific
    [InlineData(typeof(List<object>), typeof(object), true)] // a list named as a supertype of its items
    [InlineData(typeof(object), typeof(Dictionary<string, object>), false)]
    [InlineData(typeof(List<int>), typeof(string), true)] // unrelated items: a list, as it always was
    public void Whether_a_value_is_a_list_of_a_schema_type(Type dotnetType, Type schemaDotnetType, bool isList) => Assert.Equal(isList, GqlTypeInfo.IsListOf(dotnetType, schemaDotnetType));

    [Fact]
    public void A_filter_reading_a_field_off_a_list_is_a_clean_error()
    {
        // priorities is a list of Priority, not a Priority: a field read straight off it is a query
        // error, not an ArgumentException from rebuilding Priority's field over a list.
        var (schema, data) = Build();
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ authors(filter: \"priorities.score > 5\") { name } }" }, data, null, null);
        Assert.NotNull(result.Errors);
        // said as a query error, not the "Error occurred" an unexpected exception becomes
        Assert.DoesNotContain(result.Errors, e => e.Message.EndsWith("Error occurred"));
    }

    [Fact]
    public void A_bag_typed_as_its_interface_is_one_object_and_can_be_filtered_through()
    {
        var (schema, data) = Build();
        schema.Type("Priority").RemoveField("author");
        ((SchemaType<Dictionary<string, object>>)schema.Type("Priority")).AddField("author", p => (IDictionary<string, object>)p["author"], null).Returns("Author");

        Assert.False(schema.Type("Priority").GetField("author", null).ReturnType.IsList);
        var priorities = Run(schema, data, "{ priorities(filter: \"author.name == \\\"Ben\\\"\") { mergeRequest author { name } } }", "priorities");
        Assert.Equal(["!13"], priorities.Select(p => Get(p, "mergeRequest")));
    }

    [Fact]
    public void A_list_of_objects_named_as_a_bag_type_can_be_filtered_through()
    {
        var (schema, data) = Build();
        schema.Type("Author").RemoveField("priorities");
        ((SchemaType<Dictionary<string, object>>)schema.Type("Author")).AddField("priorities", a => ((List<Dictionary<string, object>>)a["priorities"]).Cast<object>(), null).Returns("Priority");

        var authors = Run(schema, data, "{ authors(filter: \"priorities.any(score > 8)\") { name } }", "authors");
        Assert.Equal(["Ada"], authors.Select(a => Get(a, "name")));
    }

    [Fact]
    public void A_bag_read_through_a_cast_can_be_filtered_through()
    {
        var (schema, data) = Build();
        schema.Type("Priority").RemoveField("author");
        ((SchemaType<Dictionary<string, object>>)schema.Type("Priority")).AddField("author", p => p["author"], null).Returns("Author");

        var priorities = Run(schema, data, "{ priorities(filter: \"author.name == \\\"Ben\\\"\") { mergeRequest } }", "priorities");
        Assert.Equal(["!13"], priorities.Select(p => Get(p, "mergeRequest")));
    }

    [Fact]
    public void Returns_after_paging_is_an_error()
    {
        // Paging has built its page types by then, from the type the field had before.
        var (schema, _) = Build();
        var field = schema.Query().ReplaceField("priorities", c => c.Priorities, null);
        field.UseOffsetPaging();
        Assert.Throws<EntityGraphQLSchemaException>(() => field.Returns("Priority"));
    }

    [Fact]
    public void Returns_after_sorting_is_an_error_and_after_filtering_is_not()
    {
        // UseSort builds its input type when added; UseFilter reads the schema type as the query runs.
        var (schema, _) = Build();
        var sorted = schema.Query().ReplaceField("priorities", c => c.Priorities, null);
        sorted.UseSort(true);
        Assert.Throws<EntityGraphQLSchemaException>(() => sorted.Returns("Priority"));

        var filtered = schema.Query().ReplaceField("authors", c => c.Authors, null);
        filtered.UseFilter();
        filtered.Returns("Author");
    }

    private static List<object?> Run(SchemaProvider<BagContext> schema, BagContext data, string query, string field)
    {
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, data, null, null);
        Assert.Null(result.Errors);
        return ((IEnumerable<object?>)((IDictionary<string, object?>)result.Data!)[field]!).ToList();
    }

    private class TwoBags
    {
        public List<Dictionary<string, object>> Items { get; } = [new() { ["a"] = 1, ["b"] = 2 }, new() { ["a"] = 5, ["b"] = 6 }];
    }

    [Fact]
    public void A_bag_over_an_interface_is_not_swapped_for_one_over_its_class()
    {
        // A over IDictionary, B over Dictionary: a Dictionary is an IDictionary, but B is no subtype of A.
        var schema = new SchemaProvider<TwoBags>();
        schema.AddType<IDictionary<string, object>>("A", null).AddField("a", x => (int)x["a"], null);
        schema.AddType<Dictionary<string, object>>("B", null).AddField("b", x => (int)x["b"], null);
        schema.Query().AddField("items", c => c.Items, null).Returns("A").UseFilter();

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ items(filter: \"a > 2\") { a } }" }, new TwoBags(), null, null);
        Assert.Null(result.Errors);
        Assert.Single((IEnumerable<object?>)((IDictionary<string, object?>)result.Data!)["items"]!);
    }

    [Fact]
    public void A_recorded_bag_type_is_kept_over_one_found_for_a_class_implementing_its_interface()
    {
        // The override is for real subtypes (Employee : Person, a type implementing a schema interface) only.
        var schema = new SchemaProvider<TwoBags>();
        var a = schema.AddType<IDictionary<string, object>>("A", null);
        schema.AddType<Dictionary<string, object>>("B", null);
        var compile = new EntityGraphQL.Compiler.EntityQuery.EqlCompileContext(
            new EntityGraphQL.Compiler.CompileContext(new ExecutionOptions(), null, new QueryRequestContext(null, null), null, null)
        );
        var bag = System.Linq.Expressions.Expression.Parameter(typeof(Dictionary<string, object>), "bag");
        compile.RecordSchemaType(bag, a, isList: false);

        Assert.Same(a, compile.SchemaTypeOf(bag, schema, null));
    }

    [Fact]
    public void Only_a_collection_or_an_element_is_recorded_for_a_call()
    {
        // any() and count() over items whose schema type is backed by object are a bool and an int, not items.
        var schema = new SchemaProvider<BagContext>();
        schema.AddType<object>("Child", null);
        var compile = new EntityGraphQL.Compiler.EntityQuery.EqlCompileContext(
            new EntityGraphQL.Compiler.CompileContext(new ExecutionOptions(), null, new QueryRequestContext(null, null), null, null)
        );
        var items = System.Linq.Expressions.Expression.Parameter(typeof(IEnumerable<object>), "items");
        compile.RecordSchemaType(items, schema.Type("Child"), isList: true);
        var count = EntityGraphQL.Compiler.EntityQuery.EntityQueryCompiler.CompileWith("count()", items, schema, new QueryRequestContext(null, null), compile).ExpressionResult;

        Assert.False(compile.SchemaTypes.ContainsKey(count));
    }

    [Fact]
    public void Returns_keeps_the_fields_own_dotnet_type()
    {
        var (schema, _) = Build();
        var field = schema.Query().GetField("priorities", null);
        Assert.Equal(typeof(List<Dictionary<string, object>>), field.ReturnType.TypeDotnet);
        Assert.Equal("Priority", field.ReturnType.SchemaType.Name);
        Assert.True(field.ReturnType.IsList);
    }
}
