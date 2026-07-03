using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests.Aggregate;

public class AggregateRegressionTests
{
    public class ScoreService
    {
        public int Score(int id) => id * 10;
    }

    [Fact]
    public void RootServiceReductionWithArgs()
    {
        // A root scalar service reduction WITH field arguments. Before the reduction interception this ran
        // single-pass in memory (args handled by Field.GetExpression). The interception must not break it.
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Query()
            .AddField("scoreSum", new { minId = 0 }, "sum of scores over a min id")
            .Resolve<ScoreService>((ctx, args, svc) => ctx.People.Where(p => p.Id >= args.minId).Sum(p => svc.Score(p.Id)));

        var services = new ServiceCollection();
        services.AddSingleton(new ScoreService());
        var data = new TestDataContext { People = [new Person { Id = 2 }, new Person { Id = 5 }] };

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ scoreSum(minId: 3) }" }, data, services.BuildServiceProvider(), null);

        Assert.Null(result.Errors);
        Assert.Equal(50, (int)result.Data!["scoreSum"]!); // only id=5
    }

    [Fact]
    public void TwoFieldsSameElementTypeQueryableFirstThenEnumerable()
    {
        // ProjectAggregate is cached per element type. First UseAggregate (IQueryable) builds it with Queryable
        // methods / IQueryable leaf params; the second field is a List. The List's aggregate must still work.
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("queryableProjects", ctx => ctx.QueryableProjects, "Queryable projects").UseAggregate(AggregatePlacement.SiblingField);
        schema.Query().ReplaceField("projects", ctx => ctx.Projects, "List projects").UseAggregate(AggregatePlacement.SiblingField);

        var data = new TestDataContext { Projects = [new Project { Id = 1 }, new Project { Id = 4 }] };

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ projectsAggregate { count max { id } } queryableProjectsAggregate { count } }" }, data, null, null);

        Assert.Null(result.Errors);
        dynamic agg = result.Data!["projectsAggregate"]!;
        Assert.Equal(2, (int)agg.count);
        Assert.Equal(4, (int)agg.max.id);
        dynamic qagg = result.Data!["queryableProjectsAggregate"]!;
        Assert.Equal(2, (int)qagg.count);
    }
}
