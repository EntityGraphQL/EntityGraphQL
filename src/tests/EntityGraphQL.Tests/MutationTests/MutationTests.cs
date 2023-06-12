using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Collections.Generic;
using System;

namespace EntityGraphQL.Tests
{
    public class MutationTests
    {
        [Fact]
        public void MissingRequiredVar()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String!) {
                  addPerson(name: $name) { id name }
                }",
                Variables = new QueryVariables { { "na", "Frank" } }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.NotNull(res.Errors);
            var err = Enumerable.First(res.Errors);
            Assert.Equal("Missing required variable 'name' on operation 'AddPerson'", err.Message);
        }

        [Fact]
        public void SupportsMutationOptional()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
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
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
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
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
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
        public void SupportsMutationExpressionWithTask()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($names: [String]) {
  addPersonNamesAsync(names: $names) {
    id name lastName
  }
}",
                Variables = new QueryVariables {
                    {"names", new [] {"Bill", "Frank"}}
                }
            };
            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic addPersonResult = results.Data["addPersonNamesAsync"];
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
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
            var results = schemaProvider.ExecuteRequestWithContext(new QueryRequest
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
            results = schemaProvider.ExecuteRequestWithContext(new QueryRequest
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom<PeopleMutations>();
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
            var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Single(result.Errors);
            Assert.Equal("Field 'addPersonInput' - Supplied variable 'names' can not be applied to defined variable type '[String]'", result.Errors.First().Message);
        }

        [Fact]
        public void TestErrorOnVariableTypeMismatch2()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom<PeopleMutations>();
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
            var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Single(result.Errors);
            Assert.Equal("Variable or value used for argument 'nameInput' does not match argument type 'InputObject' on field 'addPersonInput'", result.Errors.First().Message);
        }

        [Fact]
        public void SupportsMutationVariablesAnonObject()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom<PeopleMutations>();
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
            var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom<PeopleMutations>();
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
            var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddInputType<InputObject>("InputObject", "Using an object in the arguments");
            schemaProvider.AddMutationsFrom<PeopleMutations>();
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
            var result = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
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
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic addPersonResult = results.Data["addPersonAdv"];
            // we only have the fields requested
            Assert.Equal(3, addPersonResult.GetType().GetFields().Length);
            Assert.NotNull(addPersonResult.GetType().GetField("id"));
            Assert.NotNull(addPersonResult.GetType().GetField("projects"));
            Assert.Equal(1, Enumerable.Count(addPersonResult.projects));
            Assert.Equal("Bill", addPersonResult.name);
        }
        [Fact]
        public void SupportsSelectionFromConstantList()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
          addPersonAdvList(name: $name) {
            id name projects { id }
          }
        }",
                Variables = new QueryVariables {
                            {"name", "Bill"}
                        }
            };
            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic addPersonResults = results.Data["addPersonAdvList"];
            var addPersonResult = Enumerable.First(addPersonResults);
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.UpdateType<Person>(type =>
            {
                type.AddField("age", "Person's age")
                    .ResolveWithService<AgeService>((p, service) => service.GetAge(p.Birthday));
            });
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
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
                }",
                Variables = new QueryVariables {
                    {"name", "Bill"}
                }
            };
            var serviceCollection = new ServiceCollection();
            var service = new AgeService();
            serviceCollection.AddSingleton(service);

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, serviceCollection.BuildServiceProvider(), null);
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.UpdateType<Person>(type =>
            {
                type.AddField("age", "Person's age")
                    .ResolveWithService<AgeService>((p, service) => service.GetAge(p.Birthday));
            });
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
                    addPersonReturnAll(name: $name) {
                        id age
                    }
                }",
                Variables = new QueryVariables {
                    {"name", "Bill"}
                }
            };
            var serviceCollection = new ServiceCollection();
            var service = new AgeService();
            serviceCollection.AddSingleton(service);

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, serviceCollection.BuildServiceProvider(), null);
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
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
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
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
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson {
          doGreatThing
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThing"];
            Assert.True((bool)result);
        }

        [Fact]
        public void TestAsyncMutationWithoutAsyncKeyword()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson {
                    doGreatThingWithoutAsyncKeyword
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThingWithoutAsyncKeyword"];
            Assert.True((bool)result);
        }

        [Fact]
        public void TestUnnamedMutationOp()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          doGreatThing
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThing"];
            Assert.True((bool)result);
        }

        [Fact]
        public void TestMethodDefaultValue()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });

            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
                    defaultValueTest
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["defaultValueTest"];
            Assert.Equal(8, result);

            //check default value in SDL
            var sdl = schemaProvider.ToGraphQLSchemaString();
            Assert.Contains("defaultValueTest(valueWithDefault: Int! = 8): Int!", sdl);

            //check default value in introspection
            gql = new QueryRequest
            {
                Query = @"
                {
	                __schema {
                        mutationType {
                            fields {
                                name
                                args {
                                    name
                                    defaultValue
                                }
                            }
                        }
                    }
                }"
            };

            results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

            Assert.Null(results.Errors);
            result = results.Data["__schema"];

            var field = ((IEnumerable<dynamic>)result.mutationType.fields).Where(x => x.name == "defaultValueTest");
            Assert.Contains("8", field.First().args[0].defaultValue as string);
        }

        [Fact]
        public void TestRequiredGuid()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          needsGuid
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Single(results.Errors);
            Assert.Equal("Field 'needsGuid' - missing required argument 'id'", results.Errors[0].Message);
        }



        [Fact]
        public void TestRegExValidationAttribute()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            var gql = new QueryRequest
            {
                Query = @"mutation {
                regexValidation(title: ""name"" author: ""steve"" ) 
            }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Single(results.Errors);
            Assert.Equal("Field 'regexValidation' - Title does not match required format", results.Errors[0].Message);
        }

        [Fact]
        public void TestRegExValidationAttribute2()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            var gql = new QueryRequest
            {
                Query = @"mutation {
                regexValidation(title: ""neme"" author: ""stave"" ) 
            }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Single(results.Errors);
            Assert.Equal("Field 'regexValidation' - Author does not match required format", results.Errors[0].Message);
        }


        [Fact]
        public void TestNonNullIsRequired()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
                    needsGuidNonNull
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'needsGuidNonNull' - missing required argument 'id'", results.Errors[0].Message);
        }
        [Fact]
        public void TestListArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
          taskWithList(inputs: [{name: ""Bill""}, {name: ""Bob""}])
          taskWithListSeparateArg(inputs: [{name: ""Bill""}, {name: ""Bob""}])
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(true, results.Data["taskWithList"]);
            Assert.Equal(true, results.Data["taskWithListSeparateArg"]);
        }

        [Fact]
        public void TestListArgInputTypeUsingVariables()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation Mutate($var: [InputObject!]) {
                    taskWithList(inputs: $var)
                }",
                Variables = new QueryVariables {
                    {"var", "[{name: \"Bill\"}, {name: \"Bob\"}]"}
                }
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(true, results.Data["taskWithList"]);
        }

        [Fact]
        public void
        TestListIntArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
                    taskWithListInt(inputs: [{id: 1, idLong: 1}, {id: 20, idLong:20}])
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(true, results.Data["taskWithListInt"]);
        }
        [Fact]
        public void TestFloatArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            schemaProvider.AddInputType<FloatInput>("FloatInput", "FloatInput").AddAllFields();
            var gql = new QueryRequest
            {
                Query = @"mutation {
          addFloat(float: 1.3)
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(1.3f, results.Data["addFloat"]);
        }
        [Fact]
        public void TestDoubleArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            schemaProvider.AddInputType<DoubleInput>("DoubleInput", "DoubleInput").AddAllFields();
            var gql = new QueryRequest
            {
                Query = @"mutation {
                    addDouble(double: 1.3)
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(1.3d, results.Data["addDouble"]);
        }
        [Fact]
        public void TestDecimalArgInputType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            schemaProvider.AddInputType<DecimalInput>("DecimalInput", "DecimalInput").AddAllFields();
            var gql = new QueryRequest
            {
                Query = @"mutation {
          addDecimal(decimal: 1.3)
        }
        ",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(1.3m, results.Data["addDecimal"]);
        }
        [Fact]
        public void TestComplexReturn()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
            schemaProvider.AddInputType<DecimalInput>("DecimalInput", "DecimalInput").AddAllFields();
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson {
                    addPerson(name: ""Luke"") { id name projects { name } }
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            var person = (dynamic)results.Data["addPerson"];
            Assert.NotNull(person);
        }
        [Fact]
        public void TestStaticMethod()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Mutation().AddFrom<PeopleMutations>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation StaticTest {
                    doGreatThingStaticly
                }",
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThingStaticly"];
            Assert.True((bool)result);
        }
        [Fact]
        public void TestClassMethod()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
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
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            dynamic result = results.Data["doGreatThingsHere"];
            Assert.True((bool)result);
        }
        [Fact]
        public void TestNoArgMutationWithService()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Mutation().Add("noArgsWithService", (AgeService ageService) =>
            {
                return ageService != null;
            });
            var sdl = schema.ToGraphQLSchemaString();
            Assert.Contains("noArgsWithService: Boolean!", sdl);
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation {
                    noArgsWithService
                }",
            };

            var serviceCollection = new ServiceCollection();
            var service = new AgeService();
            serviceCollection.AddSingleton(service);

            var testSchema = new TestDataContext();
            var results = schema.ExecuteRequestWithContext(gql, testSchema, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(results.Errors);
            Assert.Equal(true, results.Data["noArgsWithService"]);
            Assert.Empty(schema.Mutation().SchemaType.GetField("noArgsWithService", null).Arguments);
        }

        [Fact]
        public void TestNullableGuid()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            var gql = new QueryRequest
            {
                Query = @"mutation Mute($id: ID, $int: Int, $float: Float, $double: Float, $bool: Boolean, $enum: Gender) {
                    nullableGuidArgs(id: $id, int: $int, float: $float, double: $double, bool: $bool, enum: $enum)
                }",
                Variables = new QueryVariables {
                    { "id", (object)null } ,
                    { "float", (object)null } ,
                    { "double", (object)null } ,
                    { "int", (object)null } ,
                    { "bool", (object)null } ,
                    { "enum", (object)null } ,
                },
            };

            var serviceCollection = new ServiceCollection();
            var service = new AgeService();
            serviceCollection.AddSingleton(service);

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(results.Errors);
            Assert.Equal(true, results.Data["nullableGuidArgs"]);
        }

        [Fact]
        public void TestNullableGuidEmptyString()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            var gql = new QueryRequest
            {
                Query = @"mutation Mute($id: ID) {
                    nullableGuidArgs
                }",
                Variables = new QueryVariables { { "id", "" } },
            };

            var serviceCollection = new ServiceCollection();
            var service = new AgeService();
            serviceCollection.AddSingleton(service);

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, serviceCollection.BuildServiceProvider(), null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'nullableGuidArgs' - Supplied variable 'id' can not be applied to defined variable type 'ID'", results.Errors[0].Message);
        }

        [Fact]
        public void TestListGuidTypeUsingVariables()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation Mutate($ids: [ID]) {
                    listOfGuidArgs(ids: $ids)
                }",
                Variables = new QueryVariables {
                    {"ids", new string[] { "cc3e20f9-9dbb-4ded-8072-6ab3cf0c94da" } }
                }
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            IEnumerable<string> result = (IEnumerable<string>)results.Data["listOfGuidArgs"];
            Assert.True(new List<string> { "cc3e20f9-9dbb-4ded-8072-6ab3cf0c94da" }.All(i => result.Contains(i)));
        }

        [Fact]
        public void TestListGuidTypeUsingVariablesRequired()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation Mutate($ids: [ID!]!) {
                    listOfGuidArgs(ids: $ids)
                }",
                Variables = new QueryVariables {
                    {"ids", new string[] { "cc3e20f9-9dbb-4ded-8072-6ab3cf0c94da" } }
                }
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            IEnumerable<string> result = (IEnumerable<string>)results.Data["listOfGuidArgs"];
            Assert.True(new List<string> { "cc3e20f9-9dbb-4ded-8072-6ab3cf0c94da" }.All(i => result.Contains(i)));
        }

        [Fact]
        public void ConstOnMutationArgOrInpoutTypeNotAdded()
        {
            var schema = SchemaBuilder.Create<TestDataContext>();
            schema.Mutation().Add(MuationWithInputTypeConst, new SchemaBuilderOptions { AutoCreateInputTypes = true });

            Assert.DoesNotContain(schema.Mutation().SchemaType.GetFields().First(f => f.Name == "muationWithInputTypeConst").Arguments, f => f.Key == "isConst");

            Assert.Contains(schema.Type<InputWithConst>().GetFields(), f => f.Name == "name");
            Assert.DoesNotContain(schema.Type<InputWithConst>().GetFields(), f => f.Name == "inputConst");
        }

        private bool MuationWithInputTypeConst([GraphQLArguments] ArgsWithConst input)
        {
            return true;
        }

        private class ArgsWithConst
        {
            public string Name { get; set; }
            public const bool IsConst = true;
            public InputWithConst Input { get; set; }
        }

        private class InputWithConst
        {
            public string Name { get; set; }
            public const bool InputConst = true;
        }

        private bool DoGreatThingsHere()
        {
            return true;
        }

        [Fact]
        public void TestAddFromMultipleClassesImplementingInterface()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Mutation().AddFrom<IMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });

            Assert.Equal(32, schemaProvider.Mutation().SchemaType.GetFields().Count());
        }

        public class NonAttributeMarkedMethod
        {
            public Person AddPerson(PeopleMutationsArgs args)
            {
                return new Person { Name = string.IsNullOrEmpty(args.Name) ? "Default" : args.Name, Id = 555, Projects = new List<Project>() };
            }
        }

        [Fact]
        public void TestAddFromMultipleClassesImplementingInterfaceByDefault()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Mutation().AddFrom<NonAttributeMarkedMethod>();


            Assert.Empty(schemaProvider.Mutation().SchemaType.GetFields());
        }

        [Fact]
        public void TestAddFromMultipleClassesImplementingInterfaceWhenEnabled()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Mutation().AddFrom<NonAttributeMarkedMethod>(new SchemaBuilderOptions { AddNonAttributedMethodsInControllers = true });

            Assert.NotEmpty(schemaProvider.Mutation().SchemaType.GetFields());
        }

        public class MutationClassInstantiationTest
        {
            private int _value;

            public MutationClassInstantiationTest() { }

            public MutationClassInstantiationTest(int value)
            {
                _value = value;
            }

            public int GetValue()
            {
                return _value;
            }
        }

        [Fact]
        public void TestRightValueReturnedFromActivatorCreateMutationClass()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.Mutation().AddFrom<MutationClassInstantiationTest>(new SchemaBuilderOptions { AddNonAttributedMethodsInControllers = true });

            var gql = new QueryRequest
            {
                Query = @"mutation getValue() {
                    getValue()
                }"
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
            Assert.Equal(0, results.Data["getValue"]);
        }

        [Fact]
        public void TestNoArgsOnInputType()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddInputType<InputObject>("InputObject", "Using an object in the arguments");

            var ex = Assert.Throws<EntityQuerySchemaException>(() => schema.Type<InputObject>().AddField("invalid", new { id = (int?)null }, (ctx, args) => 8, "Invalid field"));
            Assert.Equal($"Field invalid on type InputObject has arguments but is a GraphQL {nameof(GqlTypes.InputObject)} type and can not have arguments.", ex.Message);
        }
    }
}