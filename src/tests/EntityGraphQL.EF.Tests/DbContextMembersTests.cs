using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class DbContextMembersTests
{
    [Fact]
    public void TestDbContextOwnMembersAreNotFields()
    {
        // left out by the type that declares them - removing their names from IgnoreProps does not expose them
        foreach (var options in new[] { new SchemaBuilderOptions(), new SchemaBuilderOptions { IgnoreProps = [] } })
        {
            var schema = SchemaBuilder.FromObject<TestDbContext>(options);

            foreach (var name in new[] { "database", "model", "changeTracker", "contextId" })
                Assert.False(schema.Query().HasField(name, null), $"{name} should not be in the schema");
            Assert.True(schema.Query().HasField("movies", null));
        }
    }
}
