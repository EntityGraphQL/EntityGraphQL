using Xunit;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Tests
{
    public class MutationArgsTests
    {
        [Fact]
        public void SupportsGenericClassArg()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddInputType<Human>("HumanInput").AddAllFields();
            schema.Mutation().Add("addPerson", ([GraphQLArguments] Partial<Human> args) => 65);

            var sdl = schema.ToGraphQLSchemaString();
            Assert.Contains("addPerson(others: HumanInput, name: String): Int!", sdl);

            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson {
                  addPerson(name: ""Herb"" others: { age: 43 })
                }",
            };
            var res = schema.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
            Assert.Equal(65, res.Data["addPerson"]);
        }

        [Fact]
        public void SupportsGenericClassArgAsInputType()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Mutation().Add("addPerson", (Partial<Human> args) => 65, new SchemaBuilderMethodOptions
            {
                AutoCreateInputTypes = true,
            });

            var sdl = schema.ToGraphQLSchemaString();
            Assert.Contains("addPerson(args: PartialHuman): Int!", sdl);
            Assert.Contains("input PartialHuman {", sdl);

            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson {
                  addPerson(args: { name: ""Herb"", others: { age: 43 } })
                }",
            };
            var res = schema.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
            Assert.Equal(65, res.Data["addPerson"]);
        }
    }

    internal class Human
    {
        public int Age { get; set; }
    }

    internal class Partial<T>
    {
        public T Others { get; set; }
        public string Name { get; set; }
    }
}