using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

/// <summary>
/// Query limits are validated after parse and before any compilation or execution - a rejected
/// query must never reach the database. The limits exist for DoS protection; issuing the EF query
/// before rejecting would defeat them.
/// </summary>
public class QueryLimitsEFTests
{
    [Fact]
    public void LimitRejectedQuery_IssuesNoSql()
    {
        var sqlLog = new List<string>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext(b => (DbContextOptionsBuilder<TestDbContext>)b.LogTo(s => sqlLog.Add(s), [DbLoggerCategory.Database.Command.Name]));
        data.Movies.Add(new Movie("Alien") { Id = 1 });
        data.SaveChanges();

        var schema = SchemaBuilder.FromObject<TestDbContext>();
        var services = new ServiceCollection();
        services.AddSingleton(data);
        sqlLog.Clear();

        // depth 4 > limit 3 - must be rejected before the EF (pass 1) query runs
        var gql = new QueryRequest { Query = "{ movies { actors { movies { name } } } }" };
        var res = schema.ExecuteRequest(gql, services.BuildServiceProvider(), null, new ExecutionOptions { MaxQueryDepth = 3 });

        Assert.NotNull(res.Errors);
        Assert.Contains(res.Errors!, e => e.Message.Contains("maximum allowed depth"));
        Assert.Null(res.Data);
        // no SQL was issued - the database pass never started
        Assert.DoesNotContain(sqlLog, s => s.Contains("SELECT"));
    }
}
