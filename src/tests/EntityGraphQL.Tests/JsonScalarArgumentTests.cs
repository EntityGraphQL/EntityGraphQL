using System;
using System.Collections.Generic;
using System.Text.Json;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// A custom scalar whose value can be any JSON - an object, a list - written as a literal in the query
/// or passed as a variable. Its type converter decides what it is, not the shape the literal has: an
/// object literal for a scalar argument is not an input object to build.
/// </summary>
public class JsonScalarArgumentTests
{
    private class Context { }

    private class EchoArgs
    {
        public JsonElement? Value { get; set; }
    }

    private static SchemaProvider<Context> Schema()
    {
        var schema = new SchemaProvider<Context>();
        schema.AddScalarType<JsonElement>("JSON", "Any JSON value");
        schema.AddCustomTypeConverter<JsonElement>((value, _) => value is JsonElement element ? element : JsonSerializer.SerializeToElement(value));
        schema.Query().AddField("echo", new EchoArgs(), "Gives back what it was given").Resolve((_, args) => args.Value);
        return schema;
    }

    private static string Echo(string query, QueryVariables? variables = null)
    {
        var result = Schema().ExecuteRequestWithContext(new QueryRequest { Query = query, Variables = variables }, new Context(), null, null);
        Assert.True(result.Errors == null, string.Join(" | ", result.Errors?.ConvertAll(e => e.Message) ?? []));
        return JsonSerializer.Serialize(((IDictionary<string, object?>)result.Data!)["echo"]);
    }

    [Fact]
    public void An_object_literal_is_the_scalars_value() =>
        Assert.Equal("""{"status":"open","take":5}""", Echo("""{ echo(value: { status: "open", take: 5 }) }"""));

    [Fact]
    public void A_list_literal_is_the_scalars_value() => Assert.Equal("""[1,"two"]""", Echo("""{ echo(value: [1, "two"]) }"""));

    [Fact]
    public void A_variable_is_the_scalars_value() =>
        Assert.Equal("""{"status":"open"}""", Echo("query Q($v: JSON) { echo(value: $v) }",
            new QueryVariables { ["v"] = JsonDocument.Parse("""{"status":"open"}""").RootElement }));

    [Fact]
    public void A_variable_inside_an_object_literal_is_the_variables_value() =>
        Assert.Equal("""{"status":"open","take":[5]}""", Echo("query Q($s: String, $t: Int) { echo(value: { status: $s, take: [$t] }) }", new QueryVariables { ["s"] = "open", ["t"] = 5 }));

    private class Code
    {
        public string Value = "";
    }

    private class CodeArgs
    {
        public Code? Code { get; set; }
    }

    [Fact]
    public void A_converter_for_a_scalar_still_gets_a_variables_string()
    {
        // only an object or list reaches a converter as the JSON it was sent as
        var schema = new SchemaProvider<Context>();
        schema.AddScalarType<Code>("Code", "A code");
        schema.AddCustomTypeConverter<Code>((value, _) => new Code { Value = (string)value! });
        schema.Query().AddField("code", new CodeArgs(), "Gives back the code").Resolve((_, args) => args.Code!.Value);

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "query Q($c: Code) { code(code: $c) }", Variables = new QueryVariables { ["c"] = JsonDocument.Parse("\"abc\"").RootElement } },
            new Context(),
            null,
            null
        );

        Assert.True(result.Errors == null, string.Join(" | ", result.Errors?.ConvertAll(e => e.Message) ?? []));
        Assert.Equal("abc", ((IDictionary<string, object?>)result.Data!)["code"]);
    }

    private class Filter
    {
        public string? Name { get; set; }
        public List<int>? Ids { get; set; }
    }

    private class FilterArgs
    {
        public Filter? Filter { get; set; }
    }

    private class FilterMutations
    {
        public string Find([GraphQLArguments] FilterArgs args) => $"{args.Filter!.Name}:{string.Join(",", args.Filter.Ids!)}";
    }

    [Fact]
    public void A_variable_inside_an_input_object_literal_is_the_variables_value()
    {
        var schema = new SchemaProvider<Context>();
        schema.AddInputType<Filter>("Filter", "A filter").AddAllFields();
        schema.Query().AddField("find", new FilterArgs(), "Echoes the filter").Resolve((_, args) => $"{args.Filter!.Name}:{string.Join(",", args.Filter.Ids!)}");
        schema.Mutation().Add("find", new Func<FilterArgs, string>(new FilterMutations().Find));
        var variables = new QueryVariables { ["n"] = "open", ["i"] = 2 };

        foreach (var query in new[] { "query Q($n: String, $i: Int) { find(filter: { name: $n, ids: [1, $i] }) }", "mutation M($n: String, $i: Int) { find(filter: { name: $n, ids: [1, $i] }) }" })
        {
            var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query, Variables = variables }, new Context(), null, null);
            Assert.True(result.Errors == null, string.Join(" | ", result.Errors?.ConvertAll(e => e.Message) ?? []));
            Assert.Equal("open:1,2", ((IDictionary<string, object?>)result.Data!)["find"]);
        }
    }

    private class Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    private class PointArgs
    {
        public Point? At { get; set; }
    }

    [Fact]
    public void An_input_type_with_a_converter_is_still_built_from_an_object()
    {
        // the converter is for another way to write one ("1,2"), not for an object
        var schema = new SchemaProvider<Context>();
        schema.AddInputType<Point>("Point", "A point").AddAllFields();
        schema.AddCustomTypeConverter<Point>(
            (value, _) =>
            {
                var parts = ((string)value!).Split(',');
                return new Point { X = int.Parse(parts[0]), Y = int.Parse(parts[1]) };
            }
        );
        schema.Query().AddField("where", new PointArgs(), "Gives back the point").Resolve((_, args) => $"{args.At!.X},{args.At.Y}");

        foreach (
            var request in new[]
            {
                new QueryRequest { Query = "{ where(at: { x: 1, y: 2 }) }" },
                new QueryRequest
                {
                    Query = "query Q($p: Point) { where(at: $p) }",
                    Variables = new QueryVariables { ["p"] = JsonDocument.Parse("""{"x":1,"y":2}""").RootElement },
                },
            }
        )
        {
            var result = schema.ExecuteRequestWithContext(request, new Context(), null, null);
            Assert.True(result.Errors == null, string.Join(" | ", result.Errors?.ConvertAll(e => e.Message) ?? []));
            Assert.Equal("1,2", ((IDictionary<string, object?>)result.Data!)["where"]);
        }
    }

    private class Search
    {
        public string? Name { get; set; } = "everyone";
    }

    private class SearchArgs
    {
        public Search? Search { get; set; }
    }

    [Fact]
    public void A_variable_not_sent_leaves_its_input_field_unset()
    {
        // as if the field was not written: its default applies, and it is not marked as set
        var schema = new SchemaProvider<Context>();
        schema.AddInputType<Search>("Search", "A search").AddAllFields();
        schema.Query().AddField("search", new SearchArgs(), "Gives back the name").Resolve((_, args) => args.Search!.Name);

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "query Q($n: String) { search(search: { name: $n }) }" }, new Context(), null, null);

        Assert.True(result.Errors == null, string.Join(" | ", result.Errors?.ConvertAll(e => e.Message) ?? []));
        Assert.Equal("everyone", ((IDictionary<string, object?>)result.Data!)["search"]);
    }
}
