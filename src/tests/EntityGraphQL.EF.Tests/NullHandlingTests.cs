using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Tests.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class NullHandlingTests
{
    [Fact]
    public void Uses_SelectWithNullCheck_ForExecuteServiceFieldsSeparatelyIsFalse()
    {
        var schema = SchemaBuilder.FromObject<NullTestContext>();
        var context = new NullTestContext();
        var gql = new QueryRequest
        {
            Query =
                @"query { 
                    texts { value }    
                }",
        };
        // if ExecuteServiceFieldsSeparately = false this means one execution regardless of service fields. Basically used if
        // your data is in memory. We can use SelectWithNullCheck and friends which EF does not support
        var result = schema.ExecuteRequestWithContext(
            gql,
            context,
            null,
            null,
            new ExecutionOptions
            {
                ExecuteServiceFieldsSeparately = false,
                BeforeExecuting = (Expression expr, bool isFinal) =>
                {
                    var compiledExpr = AssertExpression.Call(
                        null,
                        nameof(EnumerableExtensions.ToListWithNullCheck),
                        AssertExpression.Call(null, nameof(EnumerableExtensions.SelectWithNullCheck), AssertExpression.MemberBinding("Tests", AssertExpression.Any()), AssertExpression.Any()),
                        AssertExpression.Constant(false)
                    );
                    AssertExpression.Matches(compiledExpr, expr);
                    return expr;
                }
            }
        );

        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);
        var texts = result.Data!["texts"] as IEnumerable<Text>;
        Assert.Null(texts);
    }

    [Fact]
    public void DoesNotUse_SelectWithNullCheck_ForExecuteServiceFieldsSeparatelyIsTrueNoService()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        var gql = new QueryRequest
        {
            Query =
                @"query { 
                    movies { name }    
                }",
        };
        // if ExecuteServiceFieldsSeparately = true this means two executions one without service fields, the next with. The first one
        // will avoid SelectWithNullCheck and friends to allow EF to translate the query to SQL. EF etc handles nulls in the query
        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            null,
            null,
            new ExecutionOptions
            {
                ExecuteServiceFieldsSeparately = true,
                BeforeExecuting = (Expression expr, bool isFinal) =>
                {
                    var compiledExpr = AssertExpression.Call(
                        null,
                        nameof(Enumerable.ToList),
                        AssertExpression.Call(null, nameof(Enumerable.Select), AssertExpression.MemberBinding("movies", AssertExpression.Any()), AssertExpression.Any())
                    );
                    AssertExpression.Matches(compiledExpr, expr);
                    return expr;
                }
            }
        );

        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);
        var texts = result.Data!["movies"] as IEnumerable<Text>;
        Assert.Null(texts);
    }

    [Fact]
    public void Uses_SelectWithNullCheck_ForExecuteServiceFieldsSeparatelyIsTrueWithService()
    {
        var schema = SchemaBuilder.FromObject<TestDbContext>();
        schema.AddType<ProjectConfig>("Config").AddAllFields();
        schema.Type<Movie>().AddField("config", "Get configs if they exists").Resolve<ConfigService>((p, srv) => srv.Get(p.Id)).IsNullable(false);

        using var factory = new TestDbContextFactory();
        var data = factory.CreateContext();
        var serviceCollection = new ServiceCollection();
        var srv = new ConfigService();
        serviceCollection.AddSingleton(srv);
        serviceCollection.AddSingleton(data);

        var gql = new QueryRequest
        {
            Query =
                @"query { 
                    movies { name  config { type } }    
                }",
        };
        // if ExecuteServiceFieldsSeparately = true this means two executions one without service fields, the next with. The first one
        // will avoid SelectWithNullCheck and friends to allow EF to translate the query to SQL. EF etc handles nulls in the query
        var result = schema.ExecuteRequest(
            gql,
            serviceCollection.BuildServiceProvider(),
            null,
            new ExecutionOptions
            {
                ExecuteServiceFieldsSeparately = true,
                BeforeExecuting = (Expression expr, bool isFinal) =>
                {
                    var compiledExpr = isFinal
                        ? AssertExpression.Call(
                            null,
                            nameof(EnumerableExtensions.ToListWithNullCheck),
                            AssertExpression.Call(null, nameof(EnumerableExtensions.SelectWithNullCheck), AssertExpression.MemberBinding("movies", AssertExpression.Any()), AssertExpression.Any()),
                            AssertExpression.Constant(true)
                        )
                        : AssertExpression.Call(
                            null,
                            nameof(Enumerable.ToList),
                            AssertExpression.Call(null, nameof(Enumerable.Select), AssertExpression.MemberBinding("movies", AssertExpression.Any()), AssertExpression.Any())
                        );
                    AssertExpression.Matches(compiledExpr, expr);
                    return expr;
                }
            }
        );

        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);
        var texts = result.Data!["movies"] as IEnumerable<Text>;
        Assert.Null(texts);
    }
}

internal class Text
{
    public string Value { get; set; } = string.Empty;
}

internal class NullTestContext
{
    public IEnumerable<Text>? Texts { get; set; }
}
