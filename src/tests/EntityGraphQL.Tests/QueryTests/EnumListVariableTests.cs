using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// List-of-enum variables, including via Newtonsoft.Json custom type converters - a converter
/// registered for a base type (JToken) must match subclass values (JArray/JValue/JObject)
/// </summary>
public class EnumListVariableTests
{
    [GraphQLArguments]
    public class AddThingArgs
    {
        [GraphQLNotNull]
        public string Description { get; set; } = "";
        public Guid? CustomerId { get; set; }

        [GraphQLNotNull]
        public string Key { get; set; } = "";

        [GraphQLNotNull]
        public List<Gender> Claims { get; set; } = [];
    }

    public class ThingMutations
    {
        [GraphQLMutation]
        public static int AddThing(AddThingArgs args)
        {
            return args.Claims.Count;
        }
    }

    [Fact]
    public void MutationArgsClassWithNotNullListOfEnumVariables_FromJson()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddMutationsFrom<ThingMutations>();
        var q =
            @"{
            ""query"": ""mutation AddThing($description: String!, $key: String!, $claims: [Gender!]!, $customerId: ID) { addThing(description: $description, key: $key, claims: $claims, customerId: $customerId) }"",
            ""variables"": { ""description"": ""d"", ""key"": ""k"", ""claims"": [""Female""], ""customerId"": ""1b4082d8-171e-4a6a-9e4f-58bec9c6b3ce"" }
        }";
        var gql = JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(1, result.Data!["addThing"]);
    }

    [Fact]
    public void MutationArgsClassWithNotNullListOfEnumVariables_FromNewtonsoftJson()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddCustomTypeConverter<Newtonsoft.Json.Linq.JObject>((obj, toType, _) => obj.ToObject(toType));
        schema.AddCustomTypeConverter<Newtonsoft.Json.Linq.JToken>((obj, toType, _) => obj.ToObject(toType));
        schema.AddCustomTypeConverter<Newtonsoft.Json.Linq.JValue, string>((obj, _) => obj.ToString());
        schema.AddMutationsFrom<ThingMutations>();
        var q =
            @"{
            ""query"": ""mutation AddThing($description: String!, $key: String!, $claims: [Gender!]!, $customerId: ID) { addThing(description: $description, key: $key, claims: $claims, customerId: $customerId) }"",
            ""variables"": { ""description"": ""d"", ""key"": ""k"", ""claims"": [""Female""], ""customerId"": ""1b4082d8-171e-4a6a-9e4f-58bec9c6b3ce"" }
        }";
        var gql = Newtonsoft.Json.JsonConvert.DeserializeObject<QueryRequest>(q)!;
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null, new ExecutionOptions { IncludeDebugInfo = true });
        if (result.Errors != null)
            Assert.Fail(string.Join("\n---\n", result.Errors.Select(e => e.Message)));
        Assert.Equal(1, result.Data!["addThing"]);
    }

    [Fact]
    public void DirectConvertObjectType_JArrayToEnumList()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddCustomTypeConverter<Newtonsoft.Json.Linq.JObject>((obj, toType, _) => obj.ToObject(toType));
        schema.AddCustomTypeConverter<Newtonsoft.Json.Linq.JToken>((obj, toType, _) => obj.ToObject(toType));
        schema.AddCustomTypeConverter<Newtonsoft.Json.Linq.JValue, string>((obj, _) => obj.ToString());
        var jarray = Newtonsoft.Json.Linq.JArray.Parse(@"[""Female""]");
        var converted = EntityGraphQL.Compiler.Util.ExpressionUtil.ConvertObjectType(jarray, typeof(List<Gender>), schema);
        Assert.Equal(new List<Gender> { Gender.Female }, converted);
    }

    [Fact]
    public void MutationWithListOfEnumVariables_FromJson()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("countGenders", (List<Gender> genders) => genders.Count);
        var q =
            @"{
            ""query"": ""mutation CountGenders($genders: [Gender!]!) { countGenders(genders: $genders) }"",
            ""variables"": { ""genders"": [""Female""] }
        }";
        var gql = JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(1, result.Data!["countGenders"]);
    }
}
