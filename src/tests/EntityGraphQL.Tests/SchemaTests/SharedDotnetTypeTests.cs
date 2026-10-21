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

    private static (SchemaProvider<BagContext> Schema, BagContext Data) Build()
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
        author.AddField("priorities", a => (List<Dictionary<string, object>>)a["priorities"], null).Returns("Priority");

        priority.AddField("id", p => (int)p["id"], null);
        priority.AddField("mergeRequest", p => (string)p["mergeRequest"], null);
        priority.AddField("score", p => (int)p["score"], null);
        priority.AddField("author", p => (Dictionary<string, object>)p["author"], null).Returns("Author");

        schema.Query().AddField("authors", c => c.Authors, null).Returns("Author").UseFilter().UseSort(true);
        schema.Query().AddField("priorities", c => c.Priorities, null).Returns("Priority").UseFilter().UseSort(true);
        return (schema, data);
    }

    private static List<object?> Run(string query, string field)
    {
        var (schema, data) = Build();
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, data, null, null);
        Assert.Null(result.Errors);
        return ((IEnumerable<object?>)((IDictionary<string, object?>)result.Data!)[field]!).ToList();
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
    public void Returns_keeps_the_fields_own_dotnet_type()
    {
        var (schema, _) = Build();
        var field = schema.Query().GetField("priorities", null);
        Assert.Equal(typeof(List<Dictionary<string, object>>), field.ReturnType.TypeDotnet);
        Assert.Equal("Priority", field.ReturnType.SchemaType.Name);
        Assert.True(field.ReturnType.IsList);
    }
}
