using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using static EntityGraphQL.Schema.ArgumentHelper;
using Microsoft.Extensions.DependencyInjection;
using static EntityGraphQL.Tests.ServiceFieldTests;
using System.Reflection;
using System.Collections.Generic;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests graphql metadata
    /// </summary>
    public class MutationTests
    {
        [Fact]
        public void MissingRequiredVar()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String!) {
  addPerson(name: $name) { id name }
}",
                Variables = new QueryVariables { { "na", "Frank" } }
            };
            var res = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.NotNull(res.Errors);
            var err = Enumerable.First(res.Errors);
            Assert.Equal("Missing required variable 'name' on operation 'AddPerson'", err.Message);
        }

        [Fact]
        public void SupportsMutationOptional()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"
                mutation AddPerson($name: String) {
  addPerson(name: $name) {
    id name
  }
}",
                Variables = new QueryVariables { }
            };
            var res = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
            dynamic addPersonResult = res.Data["addPerson"];
            // we only have the fields requested
            var resultFields = ((FieldInfo[])addPersonResult.GetType().GetFields()).Select(f => f.Name);
            Assert.Equal(2, resultFields.Count());
            Assert.Contains("id", resultFields);
            Assert.Equal(555, addPersonResult.id);
            Assert.Contains("name", resultFields);
            Assert.Equal("Default", addPersonResult.name);
        }

        [Fact]
        public void SupportsMutationArrayArg()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($names: [String]) {
  addPersonNames(names: $names) {
    id name lastName
  }
}",
                Variables = new QueryVariables {
                    {"names", new [] {"Bill", "Frank"}}
                }
            };
            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic addPersonResult = results.Data["addPersonNames"];
            // we only have the fields requested
            var resultFields = ((FieldInfo[])addPersonResult.GetType().GetFields()).Select(f => f.Name);
            Assert.Equal(3, resultFields.Count());
            Assert.Contains("id", resultFields);
            Assert.Equal(11, addPersonResult.id);
            Assert.Contains("name", resultFields);
            Assert.Equal("Bill", addPersonResult.name);
            Assert.Equal("Frank", addPersonResult.lastName);
        }

        [Fact]
        public void SupportsMutationObject()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($names: [String]) {
          addPersonInput(nameInput: $names) {
            id name lastName
          }
        }",
                // object will come through as json in the request
                Variables = new QueryVariables {
                        { "names", new { name = "Lisa", lastName = "Simpson" } }
                }
            };
            var result = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Null(result.Errors);
            dynamic addPersonResult = (dynamic)result.Data["addPersonInput"];
            // we only have the fields requested
            var resultFields = ((List<FieldInfo>)Enumerable.ToList(addPersonResult.GetType().GetFields())).Select(f => f.Name);
            Assert.Equal(3, resultFields.Count());
            Assert.Contains("id", resultFields);
            Assert.Equal(0, addPersonResult.id);
            Assert.Contains("name", resultFields);
            Assert.Equal("Lisa", addPersonResult.name);
            Assert.Equal("Simpson", addPersonResult.lastName);
        }

        [Fact]
        public void SupportsSelectionFromConstant()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
          addPersonAdv(name: $name) {
            id name projects { id }
          }
        }",
                Variables = new QueryVariables {
                            {"name", "Bill"}
                        }
            };
            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            dynamic addPersonResult = results.Data;
            addPersonResult = Enumerable.First(addPersonResult);
            addPersonResult = addPersonResult.Value;
            // we only have the fields requested
            Assert.Equal(3, addPersonResult.GetType().GetFields().Length);
            Assert.Equal("id", addPersonResult.GetType().GetFields()[0].Name);
            Assert.Equal("projects", addPersonResult.GetType().GetFields()[2].Name);
            Assert.Equal(1, Enumerable.Count(addPersonResult.projects));
            Assert.Equal("Bill", addPersonResult.name);
        }

        [Fact]
        public void SupportsSelectionWithServiceFieldInFragment()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.UpdateType<Person>(type =>
            {
                type.AddField("age", p => WithService((AgeService service) => service.GetAge(p.Birthday)), "Person's age");
            });
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
          addPersonAdv(name: $name) {
            ...frag
          }
        }
        fragment frag on Person {
            id age
        }
        ",
                Variables = new QueryVariables {
                            {"name", "Bill"}
                        }
            };
            var serviceCollection = new ServiceCollection();
            var service = new AgeService();
            serviceCollection.AddSingleton(service);

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(results.Errors);
            dynamic addPersonResult = results.Data;
            addPersonResult = Enumerable.First(addPersonResult);
            addPersonResult = addPersonResult.Value;
            // we only have the fields requested
            Assert.Equal(2, addPersonResult.GetType().GetFields().Length);
            Assert.Equal("id", addPersonResult.GetType().GetFields()[0].Name);
            Assert.Equal("age", addPersonResult.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void MutationReturnsCollectionExpressionWithService()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.UpdateType<Person>(type =>
            {
                type.AddField("age", p => WithService((AgeService service) => service.GetAge(p.Birthday)), "Person's age");
            });
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
          addPersonReturnAll(name: $name) {
            id age
          }
        }
        ",
                Variables = new QueryVariables {
                            {"name", "Bill"}
                        }
            };
            var serviceCollection = new ServiceCollection();
            var service = new AgeService();
            serviceCollection.AddSingleton(service);

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(results.Errors);
            dynamic addPersonResult = results.Data;
            addPersonResult = Enumerable.First(addPersonResult);
            addPersonResult = addPersonResult.Value;
            Assert.Equal(2, Enumerable.Count(addPersonResult));
            addPersonResult = Enumerable.First(addPersonResult);
            // we only have the fields requested
            Assert.Equal(2, addPersonResult.GetType().GetFields().Length);
            Assert.Equal("id", addPersonResult.GetType().GetFields()[0].Name);
            Assert.Equal("age", addPersonResult.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void MutationReturnsCollectionConst()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
          addPersonReturnAllConst(name: $name) {
            id
          }
        }
        ",
                Variables = new QueryVariables {
                            {"name", "Bill"}
                        }
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic addPersonResult = results.Data;
            addPersonResult = Enumerable.First(addPersonResult);
            addPersonResult = addPersonResult.Value;
            Assert.Equal(2, Enumerable.Count(addPersonResult));
            addPersonResult = Enumerable.First(addPersonResult);
            // we only have the fields requested
            Assert.Equal(1, addPersonResult.GetType().GetFields().Length);
            Assert.Equal("id", addPersonResult.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void TestAsyncMutationNonObjectReturn()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson {
          doGreatThing
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThing"];
            Assert.True((bool)result);
        }

        [Fact]
        public void TestUnnamedMutationOp()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          doGreatThing
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThing"];
            Assert.True((bool)result);
        }

        [Fact]
        public void TestRequiredGuid()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          needsGuid
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("'needsGuid' missing required argument 'id'", results.Errors[0].Message);
        }

        [Fact]
        public void TestNonNullIsRequired()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          needsGuidNonNull
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("'needsGuidNonNull' missing required argument 'id'", results.Errors[0].Message);
        }
        [Fact]
        public void TestListArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            schemaProvider.AddInputType<InputObject>("InputObject", "Input data").AddAllFields();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          taskWithList(inputs: [{name: ""Bill""}, {name: ""Bob""}])
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(true, results.Data["taskWithList"]);
        }

        [Fact]
        public void TestListIntArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            schemaProvider.AddInputType<InputObjectId>("InputObjectId", "InputObjectId").AddAllFields();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          TaskWithListInt(inputs: [{id: 1, idLong: 1}, {id: 20, idLong:20}])
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(true, results.Data["taskWithListInt"]);
        }
    }
}