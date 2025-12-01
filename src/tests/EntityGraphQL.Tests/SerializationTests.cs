using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using EntityGraphQL.Schema;
using EntityGraphQL.Tests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EntityGraphQL.AspNet.Tests;

/// <summary>
/// Tests what happens when we get common JSON types in variables
/// </summary>
public class SerializationTests
{
    [Fact]
    public void JsonNewtonsoft()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
        schemaProvider.AddMutationsFrom<PeopleMutations>();
        schemaProvider.AddCustomTypeConverter<JObject>(JObjectTypeConverter.ChangeType);
        schemaProvider.AddCustomTypeConverter<JToken>(JTokenTypeConverter.ChangeType);
        // Simulate a JSON request with JSON.NET
        // variables will end up having JObjects
        var gql = JsonConvert.DeserializeObject<QueryRequest>(
            @"
            {
                ""query"": ""mutation AddPerson($names: InputObject) {
                    addPersonInput(nameInput: $names) {
                        id name lastName birthday
                    }
                }"",
                ""variables"": {
                    ""names"": { ""name"": ""Lisa"", ""lastName"": ""Simpson"", ""birthDate"": null  }
                }
            }"
        )!;
        var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        dynamic addPersonResult = result.Data!["addPersonInput"]!;
        // we only have the fields requested
        var resultFields = ((List<FieldInfo>)Enumerable.ToList(addPersonResult.GetType().GetFields())).Select(f => f.Name);
        Assert.Equal(4, resultFields.Count());
        Assert.Contains("id", resultFields);
        Assert.Equal(0, addPersonResult.id);
        Assert.Contains("name", resultFields);
        Assert.Equal("Lisa", addPersonResult.name);
        Assert.Equal("Simpson", addPersonResult.lastName);
        Assert.Equal((string?)null, addPersonResult.birthday);
    }

    [Fact]
    public void JsonNewtonsoftArray()
    {
        // test that even though we don't know about JArray they are IEnumerable and can easily be handled
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>();
        schemaProvider.AddCustomTypeConverter<JObject>(JObjectTypeConverter.ChangeType);
        schemaProvider.AddCustomTypeConverter<JToken>(JTokenTypeConverter.ChangeType);

        var gql = JsonConvert.DeserializeObject<QueryRequest>(
            @"
            {
                ""query"": ""mutation AddPerson($ids: [ID]) {
                    listOfGuidArgs(ids: $ids)
                }"",
                ""variables"": {
                    ""ids"": [ ""cc3e20f9-9dbb-4ded-8072-6ab3cf0c94da"" ]
                }
            }"
        )!;
        var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        dynamic addPersonResult = result.Data!["listOfGuidArgs"]!;
        // we only have the fields requested
        Assert.Equal("cc3e20f9-9dbb-4ded-8072-6ab3cf0c94da", addPersonResult[0]);
    }

    [Fact]
    public void JsonNewtonsoftArray2()
    {
        // test that even though we don't know about JArray they are IEnumerable and can easily be handled
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>();
        schemaProvider.AddCustomTypeConverter<JObject>(JObjectTypeConverter.ChangeType);
        schemaProvider.AddCustomTypeConverter<JToken>(JTokenTypeConverter.ChangeType);

        var gql = JsonConvert.DeserializeObject<QueryRequest>(
            @"
            {
                ""query"": ""mutation AddPerson($ids: [ID!]!) {
                    listOfGuidArgs(ids: $ids)
                }"",
                ""variables"": {
                    ""ids"": [ ""cc3e20f9-9dbb-4ded-8072-6ab3cf0c94da"" ]
                }
            }"
        )!;
        var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        dynamic addPersonResult = result.Data!["listOfGuidArgs"]!;
        // we only have the fields requested
        Assert.Equal("cc3e20f9-9dbb-4ded-8072-6ab3cf0c94da", addPersonResult[0]);
    }

    [Fact]
    public void JsonNewtonsoftJValue()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
        schemaProvider.AddCustomTypeConverter<JObject>(JObjectTypeConverter.ChangeType);
        schemaProvider.AddCustomTypeConverter<JToken>(JTokenTypeConverter.ChangeType);
        schemaProvider.AddCustomTypeConverter<JValue>(JValueTypeConverter.ChangeType);

        var gql = JsonConvert.DeserializeObject<QueryRequest>(
            @"
            {
                ""query"": ""mutation AddPerson($name: String! $gender: Gender!) {
                    addPerson(name: $name gender: $gender) { gender }
                }"",
                ""variables"": {
                    ""name"": ""Alex"",
                    ""gender"": ""Male""
                }
            }"
        )!;

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
    }

    [Fact]
    public void TextJsonJsonElement()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
        schemaProvider.AddMutationsFrom<PeopleMutations>();
        // Simulate a JSON request with System.Text.Json
        // variables will end up having JsonElements
        var q =
            @"{
                ""query"": ""mutation AddPerson($names: InputObject) { addPersonInput(nameInput: $names) { id name lastName birthday } }"",
                ""variables"": {
                    ""names"": { ""name"": ""Lisa"", ""lastName"": ""Simpson"", ""birthDay"": null }
                }
            }";
        var gql = System.Text.Json.JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        dynamic addPersonResult = result.Data!["addPersonInput"]!;
        // we only have the fields requested
        var resultFields = ((List<FieldInfo>)Enumerable.ToList(addPersonResult.GetType().GetFields())).Select(f => f.Name);
        Assert.Equal(4, resultFields.Count());
        Assert.Contains("id", resultFields);
        Assert.Equal(0, addPersonResult.id);
        Assert.Contains("name", resultFields);
        Assert.Equal("Lisa", addPersonResult.name);
        Assert.Equal("Simpson", addPersonResult.lastName);
        Assert.Equal((string?)null, addPersonResult.birthday);
    }
}

internal static class JObjectTypeConverter
{
    public static object ChangeType(object value, Type toType, ISchemaProvider schema)
    {
        // Default JSON deserializer will deserialize child objects in QueryVariables as this JSON type
        return ((JObject)value).ToObject(toType)!;
    }
}

internal static class JTokenTypeConverter
{
    public static object ChangeType(object value, Type toType, ISchemaProvider schema)
    {
        // Default JSON deserializer will deserialize child objects in QueryVariables as this JSON type
        return ((JToken)value).ToObject(toType)!;
    }
}

internal static class JValueTypeConverter
{
    public static object ChangeType(object value, Type toType, ISchemaProvider schema)
    {
        return ((JValue)value).ToString();
    }
}
