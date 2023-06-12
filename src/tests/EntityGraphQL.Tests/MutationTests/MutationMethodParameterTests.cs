using Xunit;
using EntityGraphQL.Schema;
using System;
using System.Linq;

namespace EntityGraphQL.Tests
{
    public class MutationMethodParameterTests
    {
        [Fact]
        public void TestSeparateArguments_PrimitivesOnly()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddScalarType<DateTime>("DateTime", "");
            schemaProvider.AddScalarType<decimal>("decimal", "");
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = false });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation addPersonPrimitive($id: Int, $name: String!, $birthday: DateTime, $weight: decimal, $gender: Gender) {
                  addPersonPrimitive(id: $id, name: $name, birthday: $birthday, weight: $weight, gender: $gender) { id name }
                }",
                Variables = new QueryVariables {
                    { "id", 3 },
                    { "name", "Frank" },
                    { "birthday", DateTime.Today },
                    { "weight", 45.5 },
                    { "gender", Gender.Male }
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestSeparateArguments_PrimitivesOnly_WithInlineDefaults()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddScalarType<DateTime>("DateTime", "");
            schemaProvider.AddScalarType<decimal>("decimal", "");
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = false });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation addPersonPrimitive($birthday: DateTime, $weight: decimal, $gender: Gender) {
                  addPersonPrimitive(id: 3, name: """", birthday: $birthday, weight: $weight, gender: $gender) { id name }
                }",
                Variables = new QueryVariables {
                    { "birthday", DateTime.Today },
                    { "weight", 45.5 },
                    { "gender", Gender.Male }
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestSeparateArguments()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddInputType<InputObject>("InputObject", "");
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = false });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPersonSeparateArguments($name: String!, $names: [String!], $nameInput: InputObject, $gender: Gender) {
                  addPersonSeparateArguments(name: $name, names: $names, nameInput: $nameInput, gender: $gender) { id name }
                }",
                Variables = new QueryVariables {
                    { "name", "Frank" },
                    { "names", new [] { "Frank" } },
                    { "nameInput", null },
                    { "gender", Gender.Female }
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestSingleArgument()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddInputType<InputObject>("InputObject", "");
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = false });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPersonSingleArgument($nameInput: InputObject) {
                  addPersonSingleArgument(nameInput: $nameInput) { id name }
                }",
                Variables = new QueryVariables {
                    { "nameInput", new InputObject() { Name = "Frank" } },
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestSingleArgument_DifferentVariableName()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddInputType<InputObject>("InputObject", "");
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = false });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPersonSingleArgument($differentName: InputObject) {
                  addPersonSingleArgument(nameInput: $differentName) { id name }
                }",
                Variables = new QueryVariables {
                    { "differentName", new InputObject() { Name = "Frank" } },
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
            Assert.Equal("Frank", ((dynamic)res.Data["addPersonSingleArgument"]).name);
        }

        [Fact]
        public void TestSingleArgument_AutoAddInputTypes()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPersonSingleArgument($nameInput: InputObject) {
                  addPersonSingleArgument(nameInput: $nameInput) { id name }
                }",
                Variables = new QueryVariables {
                    { "nameInput", new InputObject() { Name = "Frank" } },
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestSingleArgument_AutoAddInputTypes_NullableNestedType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
            var schema = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("addPersonNullableNestedType(required: NestedInputObject!, optional: NestedInputObject): Person!", schema);
            Assert.Contains("input NestedInputObject {", schema);
        }

        [Fact]
        public void TestSeparateArguments_AutoAddInputTypes()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPersonSeparateArguments($name: String!, $names: [String!], $nameInput: InputObject, $gender: Gender) {
                  addPersonSeparateArguments(name: $name, names: $names, nameInput: $nameInput, gender: $gender) { id name }
                }",
                Variables = new QueryVariables {
                    { "name", "Frank" },
                    { "names", new [] { "Frank" } },
                    { "nameInput", null },
                    { "gender", Gender.Female }
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestChildArraysDontGetArguments()
        {
            var schemaProvider = new SchemaProvider<TestDataContext>();
            schemaProvider.AddScalarType<DateTime>("DateTime", "");
            schemaProvider.AddScalarType<decimal>("decimal", "");
            schemaProvider.AddScalarType<char>("char", "");
            schemaProvider.PopulateFromContext();
            schemaProvider.AddInputType<ListOfObjectsWithIds>("ListOfObjectsWithIds", "").AddAllFields();

            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });

            Assert.Empty(schemaProvider.GetSchemaType("ListOfObjectsWithIds", null).GetFields().Where(x => x.Arguments.Any()));
        }

        [Fact]
        public void TestSingleArgument_AutoAddInputTypes_NestedType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true, AddNonAttributedMethodsInControllers = true });
            // Add a argument field with a required parameter that is defined in a nested class
            var gql = new QueryRequest
            {
                Query = @"mutation AddPersonSingleArgumentNestedType($nameInput: NestedInputObject) {
                   addPersonSingleArgumentNestedType(nameInput: $nameInput) { id name }
                 }",
                Variables = new QueryVariables {
                     { "nameInput", new NestedInputObject() { Name = "Frank" } },
                 }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestSeparateArguments_DefaultSchemaBuilder_AutoAddInputTypes()
        {
            var schemaProvider = new SchemaProvider<TestDataContext>();
            schemaProvider.AddType<Person>(nameof(Person), null).AddAllFields();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPersonSeparateArguments($name: String!, $names: [String!], $nameInput: InputObject, $gender: Gender) {
                  addPersonSeparateArguments(name: $name, names: $names, nameInput: $nameInput, gender: $gender) { id name }
                }",
                Variables = new QueryVariables {
                    { "name", "Frank" },
                    { "names", new [] { "Frank" } },
                    { "nameInput", null },
                    { "gender", Gender.Female }
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestAutoAddReturnTypeOption()
        {
            // blank schema
            var schemaProvider = new SchemaProvider<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPersonSeparateArguments($name: String!, $names: [String!], $nameInput: InputObject, $gender: Gender) {
                  addPersonSeparateArguments(name: $name, names: $names, nameInput: $nameInput, gender: $gender) { id name }
                }",
                Variables = new QueryVariables {
                    { "name", "Frank" },
                    { "names", new [] { "Frank" } },
                    { "nameInput", null },
                    { "gender", Gender.Female }
                }
            };
            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
            Assert.True(schemaProvider.HasType(typeof(Person)));
        }
    }
}
