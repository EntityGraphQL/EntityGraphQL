using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text.Json;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests what happens when we get common JSON types in variables
    /// </summary>
    public class SerializationTests
    {
        [Fact]
        public void JsonNetJObject()
        {
            // var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            // schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            // schemaProvider.AddMutationFrom(new PeopleMutations());
            // // Simulate a JSON request with JSON.NET
            // // variables will end up having JObjects
            // var gql = JsonConvert.DeserializeObject<QueryRequest>(@"
            // {
            //     ""query"": ""mutation AddPerson($names: [String]) {
            //         addPersonInput(nameInput: $names) {
            //             id name lastName
            //         }
            //     }"",
            //     ""variables"": {
            //         ""names"": { ""name"": ""Lisa"", ""lastName"": ""Simpson"" }
            //     }
            // }");
            // var result = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            // Assert.Null(result.Errors);
            // dynamic addPersonResult = result.Data["addPersonInput"];
            // // we only have the fields requested
            // var resultFields = ((List<FieldInfo>)Enumerable.ToList(addPersonResult.GetType().GetFields())).Select(f => f.Name);
            // Assert.Equal(3, resultFields.Count());
            // Assert.Contains("id", resultFields);
            // Assert.Equal(0, addPersonResult.id);
            // Assert.Contains("name", resultFields);
            // Assert.Equal("Lisa", addPersonResult.name);
            // Assert.Equal("Simpson", addPersonResult.lastName);
        }

        [Fact]
        public void TextJsonJsonElement()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            // Simulate a JSON request with System.Text.Json
            // variables will end up having JsonElements
            var q = @"{
                ""query"": ""mutation AddPerson($names: InputObject) { addPersonInput(nameInput: $names) { id name lastName } }"",
                ""variables"": {
                    ""names"": { ""name"": ""Lisa"", ""lastName"": ""Simpson"" }
                }
            }";
            var gql = System.Text.Json.JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var result = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Null(result.Errors);
            dynamic addPersonResult = result.Data["addPersonInput"];
            // we only have the fields requested
            var resultFields = ((List<FieldInfo>)Enumerable.ToList(addPersonResult.GetType().GetFields())).Select(f => f.Name);
            Assert.Equal(3, resultFields.Count());
            Assert.Contains("id", resultFields);
            Assert.Equal(0, addPersonResult.id);
            Assert.Contains("name", resultFields);
            Assert.Equal("Lisa", addPersonResult.name);
            Assert.Equal("Simpson", addPersonResult.lastName);
        }
    }
}