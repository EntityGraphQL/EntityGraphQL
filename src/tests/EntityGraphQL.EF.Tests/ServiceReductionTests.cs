using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class ScoreService
{
    public int Score(int id) => id * 10;
}

public class ServiceReductionTests
{
    [Fact]
    public void ScalarServiceAggregateOverQueryable()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.Query().AddField("scoreSum", "sum of scores").Resolve<ScoreService>((db, svc) => db.Movies.Sum(m => svc.Score(m.Id)));

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        data.Movies.AddRange(new Movie("A") { Id = 2 }, new Movie("B") { Id = 5 });
        data.SaveChanges();

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ scoreSum }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors); // (2*10)+(5*10) = 70
        Assert.Equal(70, (int)result.Data!["scoreSum"]!);
    }
}
