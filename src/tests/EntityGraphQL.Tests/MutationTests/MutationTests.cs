using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
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
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
        public void TestMutationConstantParametersDoNotChange()
        {
            // This tests that whena mutation returns an Expression
            // ctx => ctx.People.First(p => p.Id == myNewPerson.Id)
            // that myNewPerson.Id does not get replace by something dumb like p.Id == p.Id
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var query = @"mutation AddPerson($names: [String]) {
  addPersonNamesExpression(names: $names) {
    id name lastName
  }
}";
            var vars1 = new QueryVariables {
                    {"names", new [] {"Bill", "Frank"}}
                };
            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(new QueryRequest
            {
                Query = query,
                Variables = vars1
            }, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic addPersonResult = results.Data["addPersonNamesExpression"];
            // we only have the fields requested
            var resultFields = ((FieldInfo[])addPersonResult.GetType().GetFields()).Select(f => f.Name);
            Assert.Equal(3, resultFields.Count());
            Assert.Contains("id", resultFields);
            Assert.Equal(0, addPersonResult.id);
            Assert.Contains("name", resultFields);
            Assert.Equal("Bill", addPersonResult.name);
            Assert.Equal("Frank", addPersonResult.lastName);

            // add another one to trigger the issue
            results = schemaProvider.ExecuteRequest(new QueryRequest
            {
                Query = query,
                Variables = new QueryVariables {
                    {"names", new [] {"Mary", "Joe"}}
                }
            }, testSchema, null, null);
            Assert.Null(results.Errors);
            addPersonResult = results.Data["addPersonNamesExpression"];
            // we only have the fields requested
            resultFields = ((FieldInfo[])addPersonResult.GetType().GetFields()).Select(f => f.Name);
            Assert.Equal(3, resultFields.Count());
            Assert.Contains("id", resultFields);
            Assert.Equal(1, addPersonResult.id);
            Assert.Contains("name", resultFields);
            Assert.Equal("Mary", addPersonResult.name);
            Assert.Equal("Joe", addPersonResult.lastName);
        }

        [Fact]
        public void TestErrorOnVariableTypeMismatch()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($names: [String]) { # wrong variable type
          addPersonInput(nameInput: $names) {
            id name lastName
          }
        }",
                // Object does not match the var definition in the AddPerson operation
                Variables = new QueryVariables {
                        { "names", new { name = "Lisa", lastName = "Simpson" } }
                }
            };
            var result = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Single(result.Errors);
            Assert.Equal("Field error: addPersonInput - Supplied variable 'names' can not be applied to defined variable type '[String]'", result.Errors.First().Message);
        }

        [Fact]
        public void TestErrorOnVariableTypeMismatch2()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($names: [String]) { # wrong variable type
          addPersonInput(nameInput: $names) {
            id name lastName
          }
        }",
                // variable matches the var definition but does not match the field expected type
                Variables = new QueryVariables {
                        { "names", new [] { "Lisa", "Simpson" } }
                }
            };
            var result = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Single(result.Errors);
            Assert.Equal("Field error: addPersonInput - Variable or value used for argument 'nameInput' does not match argument type 'InputObject'", result.Errors.First().Message);
        }

        [Fact]
        public void SupportsMutationVariablesAnonObject()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($names: InputObject) {
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
        public void SupportsMutationVariablesObject()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($names: InputObject) {
          addPersonInput(nameInput: $names) {
            id name lastName
          }
        }",
                // object will come through as json in the request
                Variables = new QueryVariables {
                        { "names", new InputObject{ Name = "Lisa", LastName = "Simpson" } }
                }
            };
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

        [Fact]
        public void SupportsMutationVariablesDictionary()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($names: InputObject) {
          addPersonInput(nameInput: $names) {
            id name lastName
          }
        }",
                // object will come through as json in the request
                Variables = new QueryVariables {
                        { "names", new Dictionary<string, object> {
                            {"name", "Lisa"},
                            {"lastName", "Simpson"}
                        }}
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
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            Assert.NotNull(addPersonResult.GetType().GetField("id"));
            Assert.NotNull(addPersonResult.GetType().GetField("projects"));
            Assert.Equal(1, Enumerable.Count(addPersonResult.projects));
            Assert.Equal("Bill", addPersonResult.name);
        }

        [Fact]
        public void SupportsSelectionWithServiceFieldInFragment()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.UpdateType<Person>(type =>
            {
                type.AddField("age", "Person's age")
                    .ResolveWithService<AgeService>((p, service) => service.GetAge(p.Birthday));
            });
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            Assert.NotNull(addPersonResult.GetType().GetField("id"));
            Assert.NotNull(addPersonResult.GetType().GetField("age"));
        }

        [Fact]
        public void MutationReturnsCollectionExpressionWithService()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.UpdateType<Person>(type =>
            {
                type.AddField("age", "Person's age")
                    .ResolveWithService<AgeService>((p, service) => service.GetAge(p.Birthday));
            });
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            Assert.NotNull(addPersonResult.GetType().GetField("id"));
            Assert.NotNull(addPersonResult.GetType().GetField("age"));
        }

        [Fact]
        public void MutationReturnsCollectionConst()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            Assert.NotNull(addPersonResult.GetType().GetField("id"));
        }

        [Fact]
        public void TestAsyncMutationNonObjectReturn()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            Assert.Equal("Field error: needsGuid - 'needsGuid' missing required argument 'id'", results.Errors[0].Message);
        }

        [Fact]
        public void TestNonNullIsRequired()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
            Assert.Equal("Field error: needsGuidNonNull - 'needsGuidNonNull' missing required argument 'id'", results.Errors[0].Message);
        }
        [Fact]
        public void TestListArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
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
        public void
        TestListIntArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            schemaProvider.AddInputType<InputObjectId>("InputObjectId", "InputObjectId").AddAllFields();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          taskWithListInt(inputs: [{id: 1, idLong: 1}, {id: 20, idLong:20}])
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(true, results.Data["taskWithListInt"]);
        }
        [Fact]
        public void TestFloatArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            schemaProvider.AddInputType<FloatInput>("FloatInput", "FloatInput").AddAllFields();
            var gql = new QueryRequest
            {
                Query = @"mutation {
          addFloat(float: 1.3)
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(1.3f, results.Data["addFloat"]);
        }
        [Fact]
        public void TestDoubleArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            schemaProvider.AddInputType<DoubleInput>("DoubleInput", "DoubleInput").AddAllFields();
            var gql = new QueryRequest
            {
                Query = @"mutation {
          addDouble(double: 1.3)
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(1.3d, results.Data["addDouble"]);
        }
        [Fact]
        public void TestDecimalArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            schemaProvider.AddInputType<DecimalInput>("DecimalInput", "DecimalInput").AddAllFields();
            var gql = new QueryRequest
            {
                Query = @"mutation {
          addDecimal(decimal: 1.3)
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(1.3m, results.Data["addDecimal"]);
        }
        [Fact]
        public void TestComplexReturn()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            schemaProvider.AddInputType<DecimalInput>("DecimalInput", "DecimalInput").AddAllFields();
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson {
                    addPerson(name: ""Luke"") { id name projects { name } }
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            var person = (dynamic)results.Data["addPerson"];
            Assert.NotNull(person);
        }
        [Fact]
        public void TestStaticMethod()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.Mutation().AddFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation StaticTest {
                    doGreatThingStaticly
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThingStaticly"];
            Assert.True((bool)result);
        }
        [Fact]
        public void TestClassMethod()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            // test example where you might have a builder class set things up
            schemaProvider.Mutation().Add("doGreatThingsHere", DoGreatThingsHere);
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation StaticTest {
                    doGreatThingsHere
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThingsHere"];
            Assert.True((bool)result);
        }

        private bool DoGreatThingsHere()
        {
            return true;
        }
    }
}