using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class DelegateKeyCollisionTest
{
    [Fact]
    public void TwoRootFields_WithDelegateCache_DoNotCollide()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var context = new TestDataContext().FillWithTestData();
        var options = new ExecutionOptions { CacheCompiledDelegates = true };
        var gql = new QueryRequest { Query = "{ people { name } projects { name } }" };
        var res = schema.ExecuteRequestWithContext(gql, context, null, null, options);
        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        dynamic projects = res.Data!["projects"]!;
        // if the compiled-delegate cache key collides across root fields, 'projects' is served the
        // 'people' delegate and returns people data
        Assert.Equal("Luke", System.Linq.Enumerable.First(people).name);
        Assert.Equal("Project 3", System.Linq.Enumerable.First(projects).name);
    }
}
