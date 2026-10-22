using System.Collections.Generic;
using System.Linq;
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

    private static (SchemaProvider<BagContext> Schema, BagContext Data) Build(bool paged = false)
    {
        var data = new BagContext();
        var ada = Author(1, "Ada");
        var ben = Author(2, "Ben");
        data.Authors.AddRange([ada, ben]);
        data.Priorities.AddRange([Priority(1, "!12", 9, ada), Priority(2, "!13", 3, ben), Priority(3, "!14", 7, ada)]);

        var schema = new SchemaProvider<BagContext>();
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
    public void Returns_keeps_the_fields_own_dotnet_type()
    {
        var (schema, _) = Build();
        var field = schema.Query().GetField("priorities", null);
        Assert.Equal(typeof(List<Dictionary<string, object>>), field.ReturnType.TypeDotnet);
        Assert.Equal("Priority", field.ReturnType.SchemaType.Name);
        Assert.True(field.ReturnType.IsList);
    }
}
