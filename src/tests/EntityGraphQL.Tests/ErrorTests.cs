using Xunit;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests graphql metadata
    /// </summary>
    public class ErrorTests
    {
        [Fact]
        public void MutationReportsError()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            schemaProvider.AddMutationsFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
  addPersonError(name: $name)
}
",
                Variables = new QueryVariables {
                    {"name", "Bill"}
                }
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field error: addPersonError - Name can not be null (Parameter 'name')", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'error' not found on type 'Person'", results.Errors[0].Message);
        }
    }
}