using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// GraphQL input coercion: a single value for a list type is a list of one - inline or from a variable
/// </summary>
public class ListInputCoercionTests
{
    private static readonly SchemaProvider<TestDataContext> Schema = SchemaBuilder.FromObject<TestDataContext>();

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void A_variable_for_an_array_is_an_array() => Assert.Equal([5], (int[])ExpressionUtil.ConvertObjectType(5, typeof(int[]), Schema)!);

    [Fact]
    public void A_json_variable_for_an_array_is_an_array() => Assert.Equal([5], (int[])ExpressionUtil.ConvertObjectType(Json("5"), typeof(int[]), Schema)!);

    [Fact]
    public void A_variable_for_a_list_of_ints_is_a_list() => Assert.Equal([5], (List<int>)ExpressionUtil.ConvertObjectType(Json("5"), typeof(List<int>), Schema)!);

    [Fact]
    public void A_variable_for_a_nested_list_is_nested() =>
        Assert.Equal([5], ((List<List<int>>)ExpressionUtil.ConvertObjectType(5, typeof(List<List<int>>), Schema)!).Single());

    [Fact]
    public void An_inline_value_for_an_array_is_an_array() => Assert.Equal([5], (int[])GraphQLParser.ConvertArgumentValue(Schema, 5, typeof(int[]))!);

    [Fact]
    public void An_inline_value_for_a_nested_list_is_nested() =>
        Assert.Equal([5], ((List<List<int>>)GraphQLParser.ConvertArgumentValue(Schema, 5, typeof(List<List<int>>))!).Single());

    [Fact]
    public void A_list_already_converted_is_not_wrapped_again()
    {
        // a variable's value substituted into an object literal - { ids: $ids } - is already a List<int>
        var ids = new List<int> { 1, 2 };
        Assert.Equal(ids, (List<int>)GraphQLParser.ConvertArgumentValue(Schema, ids, typeof(List<int>))!);
    }

    [Fact]
    public void A_dictionary_is_not_a_list_of_its_pairs()
    {
        Assert.IsType<Dictionary<string, object>>(ExpressionUtil.ConvertObjectType(Json("""{"a": 1}"""), typeof(Dictionary<string, object>), Schema));
        Assert.IsType<Dictionary<string, object>>(ExpressionUtil.ConvertObjectType(new Dictionary<string, object> { ["a"] = 1 }, typeof(Dictionary<string, object>), Schema));
    }

    [Fact]
    public void A_list_typed_scalar_is_left_to_its_converter_inline()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<List<string>>("Csv", "Comma separated values");
        schema.AddCustomTypeConverter<string, List<string>>((value, _) => [.. value.Split(',')]);
        Assert.Equal(["a", "b"], (List<string>)GraphQLParser.ConvertArgumentValue(schema, "a,b", typeof(List<string>))!);
        Assert.Equal(["a", "b"], (List<string>)ExpressionUtil.ConvertObjectType("a,b", typeof(List<string>), schema)!);
    }
}
