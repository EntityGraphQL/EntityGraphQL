using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests.Aggregate;

// Validates the general engine support (Option B): a scalar field whose resolver reduces a collection using a
// service is split into a pass-1 deps projection + pass-2 in-memory reduction.
public class ServiceReductionTests
{
    public class ScoreService
    {
        public int Calls { get; private set; }

        public int Score(int id)
        {
            Calls++;
            return id * 10;
        }
    }

    [Fact]
    public void ScalarServiceSumOverCollection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("scoreSum", "sum of scores").Resolve<ScoreService>((ctx, svc) => ctx.People.Sum(p => svc.Score(p.Id)));

        var svc = new ScoreService();
        var services = new ServiceCollection();
        services.AddSingleton(svc);
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ scoreSum }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        Assert.Equal(70, (int)result.Data!["scoreSum"]!); // (2*10)+(5*10)
        Assert.Equal(2, svc.Calls); // service invoked once per element, not re-run
    }

    [Fact]
    public void ScalarServiceMaxOverCollection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("scoreMax", "max score").Resolve<ScoreService>((ctx, svc) => ctx.People.Max(p => svc.Score(p.Id)));

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ scoreMax }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        Assert.Equal(50, (int)result.Data!["scoreMax"]!);
    }
}
