using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.QueryLimits;
using Xunit;

namespace EntityGraphQL.Tests;

public class QueryLimitsTests
{
    private static SchemaProvider<TestDataContext> BuildSchema() => SchemaBuilder.FromObject<TestDataContext>();

    [Fact]
    public void NoLimitsSet_AllowsAnyQuery()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        var gql = new QueryRequest { Query = @"{ projects { id name tasks { id name } } }" };
        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions());
        Assert.Null(result.Errors);
    }

    [Fact]
    public void MaxQueryDepth_AllowedAtBoundary()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        // depth: projects=1, tasks=2, assignee=3, manager=4, id=5
        var gql = new QueryRequest { Query = @"{ projects { tasks { assignee { manager { id } } } } }" };
        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryDepth = 5 });
        Assert.Null(result.Errors);
    }

    [Fact]
    public void MaxQueryDepth_ExceededAborts()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        var gql = new QueryRequest { Query = @"{ projects { tasks { assignee { manager { id } } } } }" };
        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryDepth = 3 });
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors!, e => e.Message.Contains("maximum allowed depth"));
    }

    [Fact]
    public void MaxQueryNodes_CountsLeafSelections()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        // 6 fields: projects + id + name + tasks + id + name
        var gql = new QueryRequest { Query = @"{ projects { id name tasks { id name } } }" };

        var pass = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryNodes = 6 });
        Assert.Null(pass.Errors);

        var fail = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryNodes = 5 });
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("maximum allowed node count"));
    }

    [Fact]
    public void MaxFieldAliases_BlocksBatchedAliasAttack()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        // simulate a batched-alias attack — same field aliased many times
        var gql = new QueryRequest
        {
            Query =
                @"{
                    a: totalPeople
                    b: totalPeople
                    c: totalPeople
                    d: totalPeople
                }",
        };

        var fail = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxFieldAliases = 3 });
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("maximum allowed alias count"));

        var pass = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxFieldAliases = 4 });
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void MaxQueryComplexity_ExceededAborts()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        var gql = new QueryRequest { Query = @"{ projects { id name tasks { id name } } }" };
        // baseline cost = 1 (projects) + (1 (id) + 1 (name) + 1 (tasks) + (1 + 1) * 1 ) * 1 = 6
        var fail = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryComplexity = 3 });
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("complexity"));
    }

    [Fact]
    public void SetComplexity_PerField_Override()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        schema.Type<TestDataContext>().GetField("totalPeople", null).SetComplexity(50);
        var gql = new QueryRequest { Query = @"{ totalPeople }" };

        var fail = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryComplexity = 49 });
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("complexity"));

        var pass = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryComplexity = 50 });
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void SetComplexity_Calculator_UsesArgsAndChildCost()
    {
        var schema = BuildSchema();
        schema.Query().ReplaceField("projects", new { take = 10 }, (ctx, args) => ctx.Projects.Take(args.take), "projects");
        // model: 1 base * (1 + childCost) * take
        schema.Query().GetField("projects", null).SetComplexity(ctx => (ctx.Arg<int>("take") + 1) * (1 + ctx.ChildComplexity));
        var data = new TestDataContext();

        // childCost = id + name = 2. take=50 -> (50+1) * (1+2) = 153
        var over = schema.ExecuteRequestWithContext(new QueryRequest { Query = @"{ projects(take: 50) { id name } }" }, data, null, null, new ExecutionOptions { MaxQueryComplexity = 100 });
        Assert.NotNull(over.Errors);
        Assert.Contains(over.Errors!, e => e.Message.Contains("complexity"));

        // take=5 -> (5+1) * (1+2) = 18
        var under = schema.ExecuteRequestWithContext(new QueryRequest { Query = @"{ projects(take: 5) { id name } }" }, data, null, null, new ExecutionOptions { MaxQueryComplexity = 100 });
        Assert.Null(under.Errors);
    }

    [Fact]
    public void SetComplexity_TypedChain_InfersTParamsFromField()
    {
        // Field's anonymous args shape flows through the chain; no sample needed.
        var schema = BuildSchema();
        schema.Query().ReplaceField("projects", new { take = 10 }, (ctx, args) => ctx.Projects.Take(args.take), "projects").SetComplexity(ctx => ctx.Args.take * (1 + ctx.ChildComplexity));
        var data = new TestDataContext();

        // take=10 -> 10 * (1 + 2) = 30
        var over = schema.ExecuteRequestWithContext(new QueryRequest { Query = @"{ projects(take: 10) { id name } }" }, data, null, null, new ExecutionOptions { MaxQueryComplexity = 20 });
        Assert.NotNull(over.Errors);

        // take=3 -> 3 * (1 + 2) = 9
        var under = schema.ExecuteRequestWithContext(new QueryRequest { Query = @"{ projects(take: 3) { id name } }" }, data, null, null, new ExecutionOptions { MaxQueryComplexity = 20 });
        Assert.Null(under.Errors);
    }

    [Fact]
    public void NoListMultiplierHeuristic_PaginationArgsAreIgnoredByDefault()
    {
        // Without a SetComplexity override, take/first/limit don't inflate the cost — the analyzer only
        // counts selections. This is a deliberate change from earlier behavior: use SetComplexity(ctx => ...)
        // if you want pagination-aware cost.
        var schema = BuildSchema();
        schema.Query().ReplaceField("projects", new { take = 10 }, (ctx, args) => ctx.Projects.Take(args.take), "projects");
        var data = new TestDataContext();
        // Cost = projects(1) + id(1) + name(1) = 3, regardless of take
        var query = @"{ projects(take: 10000) { id name } }";
        var pass = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, data, null, null, new ExecutionOptions { MaxQueryComplexity = 3 });
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void FragmentSpread_ContributesToComplexityAndNodeCount()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        // A fragment spread must be costed — otherwise attackers bypass limits via fragments.
        var query =
            @"
            query {
                a: totalPeople
                b: totalPeople
                ...Counts
            }
            fragment Counts on Query {
                c: totalPeople
                d: totalPeople
                e: totalPeople
            }";
        var gql = new QueryRequest { Query = query };

        var fail = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxFieldAliases = 4 });
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("maximum allowed alias count"));
    }

    [Fact]
    public void InlineFragment_ContributesToCostAndDepth()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        var query =
            @"{
                projects {
                    ... on Project {
                        id
                        name
                    }
                }
            }";
        var gql = new QueryRequest { Query = query };
        // 3 nodes: projects + id + name. Inline fragment contributes zero nodes itself.
        var pass = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryNodes = 3 });
        Assert.Null(pass.Errors);
        var fail = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryNodes = 2 });
        Assert.NotNull(fail.Errors);
    }

    [Fact]
    public void CustomAnalyzer_IsUsedWhenProvided()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        var analyzer = new FixedCostAnalyzer(costPerQuery: 999);
        var gql = new QueryRequest { Query = @"{ totalPeople }" };
        var fail = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryComplexity = 100, QueryComplexityAnalyzer = analyzer });
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("999"));
    }

    private sealed class FixedCostAnalyzer : IQueryComplexityAnalyzer
    {
        private readonly int cost;

        public FixedCostAnalyzer(int costPerQuery)
        {
            cost = costPerQuery;
        }

        public int CalculateComplexity(Compiler.GraphQLDocument document, string? operationName, QueryVariables? variables, ExecutionOptions options) => cost;
    }

    [Fact]
    public void SetComplexity_VariableArg_UsesRealValueNotDefault()
    {
        // $pageSize is resolved from request variables so the calculator sees 50, not 0 (the int default).
        var schema = BuildSchema();
        schema.Query().ReplaceField("projects", new { take = 10 }, (ctx, args) => ctx.Projects.Take(args.take), "projects").SetComplexity(ctx => ctx.Args.take * (1 + ctx.ChildComplexity));
        var data = new TestDataContext();

        // take=50, children id+name cost 2 → 50 * (1+2) = 150
        var gql = new QueryRequest
        {
            Query = "query Q($pageSize: Int!) { projects(take: $pageSize) { id name } }",
            Variables = new QueryVariables { { "pageSize", 50 } },
        };

        var fail = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryComplexity = 149 });
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("complexity"));

        var pass = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { MaxQueryComplexity = 150 });
        Assert.Null(pass.Errors);

        // Sanity: with $pageSize=0 the cost is 0 — limit 1 passes
        var zeroGql = new QueryRequest
        {
            Query = "query Q($pageSize: Int!) { projects(take: $pageSize) { id name } }",
            Variables = new QueryVariables { { "pageSize", 0 } },
        };
        var zeroPass = schema.ExecuteRequestWithContext(zeroGql, data, null, null, new ExecutionOptions { MaxQueryComplexity = 1 });
        Assert.Null(zeroPass.Errors);
    }
}
