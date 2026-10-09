using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class DbContextMembersTests
{
    [Fact]
    public void TestDbContextOwnMembersAreNotFields()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();

        foreach (var name in new[] { "database", "model", "changeTracker", "contextId" })
            Assert.False(schema.Query().HasField(name, null), $"{name} should not be in the schema");
        Assert.True(schema.Query().HasField("movies", null));
    }
}
