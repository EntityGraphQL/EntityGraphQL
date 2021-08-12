using Xunit;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests graphql metadata
    /// </summary>
    public class EnumTests
    {
        [Fact]
        public void EnumTest()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            var gql = new QueryRequest
            {
                Query = @"{
  people {
      gender
  }
}
",
            };

            var testSchema = new TestSchema();
            var results = schemaProvider.ExecuteQuery(gql, testSchema, null, null);
            Assert.Null(results.Errors);
        }
    }
}