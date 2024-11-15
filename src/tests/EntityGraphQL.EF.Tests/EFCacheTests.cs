using EntityGraphQL.Schema;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EntityGraphQL.EF.Tests;

public class EFCacheTests
{
    [Fact]
    public void TestEfCoreQueryCacheWithListNode()
    {
        using var factory = Setup(out TestLoggerProvider loggerProvider, out ServiceProvider serviceProvider, out SchemaProvider<TestDbContext> schema);

        // GraphQL query
        var query = new QueryRequest
        {
            Query =
                @"{
                actors {
                    id
                    name
                    movies { id }
                }
            }",
        };

        loggerProvider.Logger.ResetCounts();
        var result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);

        Assert.Null(result.Errors);
        // Assert that query compilation occurred only once
        Assert.Equal(1, loggerProvider.Logger.QueryCompilationCount);
        Assert.Equal(5, loggerProvider.Logger.QueryExecutionCount);

        var actors = result.Data!["actors"] as IEnumerable<dynamic>;
        Assert.Equal(2, actors!.Count());
        Assert.Equal(2, actors!.First(a => a.name == "Alice").movies.Count);
    }

    [Fact]
    public void TestEfCoreQueryCacheWithListToSingleNode()
    {
        using var factory = Setup(out TestLoggerProvider loggerProvider, out ServiceProvider serviceProvider, out SchemaProvider<TestDbContext> schema);

        // GraphQL query
        var query = new QueryRequest
        {
            Query =
                @"{
                actor(id: 1) {
                    id
                    name
                    movie(id: 10) { name }
                }
            }",
        };

        loggerProvider.Logger.ResetCounts();
        var result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);

        Assert.Null(result.Errors);
        // Assert that query compilation occurred only once
        Assert.Equal(1, loggerProvider.Logger.QueryCompilationCount);
        Assert.Equal(5, loggerProvider.Logger.QueryExecutionCount);

        var actor = result.Data!["actor"] as dynamic;
        Assert.Equal(1, actor!.id);
        Assert.Equal("Title 2", actor!.movie.name);
    }

    [Fact]
    public void TestEfCoreQueryCacheWithListToSingleNodeWithParams()
    {
        using var factory = Setup(out TestLoggerProvider loggerProvider, out ServiceProvider serviceProvider, out SchemaProvider<TestDbContext> schema);

        // GraphQL query
        var query = new QueryRequest
        {
            Query =
                @"query ($id: Int!) {
                actor(id: $id) {
                    id
                    name
                    birthday
                    movie(id: 10) { name }
                }
            }",
            Variables = new QueryVariables { ["id"] = 1 },
        };

        loggerProvider.Logger.ResetCounts();
        var result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);

        Assert.Null(result.Errors);
        // Assert that query compilation occurred only once
        Assert.Equal(1, loggerProvider.Logger.QueryCompilationCount);
        Assert.Equal(5, loggerProvider.Logger.QueryExecutionCount);
    }

    [Fact]
    public void TestEfCoreQueryCacheWithListToSingleNodeNotFromSchemaBuilder()
    {
        using var factory = Setup(out TestLoggerProvider loggerProvider, out ServiceProvider serviceProvider, out SchemaProvider<TestDbContext> schema);

        schema.UpdateQuery(c =>
        {
            c.ReplaceField("actor", new { id = (int?)null }, (c, args) => c.Actors.FirstOrDefault(x => x.Id == args.id), "");
        });

        // GraphQL query
        var query = new QueryRequest
        {
            Query =
                @"query ($id: Int!) {
                actor(id: $id) {
                    id
                    name
                    birthday
                }
            }",
            Variables = new QueryVariables { ["id"] = 1 },
        };

        loggerProvider.Logger.ResetCounts();
        var result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);

        Assert.Null(result.Errors);
        // Assert that query compilation occurred only once
        Assert.Equal(1, loggerProvider.Logger.QueryCompilationCount);
        Assert.Equal(5, loggerProvider.Logger.QueryExecutionCount);
    }

    [Fact]
    public void TestEfCoreQueryCacheWithListToSingleNodeNotFromSchemaBuilderNotRootField()
    {
        using var factory = Setup(out TestLoggerProvider loggerProvider, out ServiceProvider serviceProvider, out SchemaProvider<TestDbContext> schema);

        schema.UpdateType<Actor>(c =>
        {
            c.ReplaceField("movie", new { id = (int?)null }, (c, args) => c.Movies.FirstOrDefault(x => x.Id == args.id), "");
        });

        // GraphQL query
        var query = new QueryRequest
        {
            Query =
                @"query ($id: Int!) {
                actors {
                    id
                    name
                    movie(id: 10) { name }
                }
            }",
            Variables = new QueryVariables { ["id"] = 1 },
        };

        loggerProvider.Logger.ResetCounts();
        var result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);
        result = schema.ExecuteRequest(query, serviceProvider, null);

        Assert.Null(result.Errors);
        // Assert that query compilation occurred only once
        Assert.Equal(1, loggerProvider.Logger.QueryCompilationCount);
        Assert.Equal(5, loggerProvider.Logger.QueryExecutionCount);
    }

    private static TestDbContextFactory Setup(out TestLoggerProvider loggerProvider, out ServiceProvider serviceProvider, out SchemaProvider<TestDbContext> schema)
    {
        loggerProvider = new TestLoggerProvider();
        var serviceCollection = new ServiceCollection();
        var factory = new TestDbContextFactory();
        var lp = loggerProvider;
        var context = factory.CreateContext(config => config.UseLoggerFactory(new LoggerFactory([lp])).EnableSensitiveDataLogging());
        serviceCollection.AddSingleton(context);
        serviceProvider = serviceCollection.BuildServiceProvider();
        context.Actors.AddRange(new Actor("Alice") { Id = 1, Movies = [new Movie("Title 1") { Id = 9 }, new Movie("Title 2") { Id = 10 }] }, new Actor("Bob") { Id = 2 });
        context.SaveChanges();

        schema = SchemaBuilder.FromObject<TestDbContext>();
        return factory;
    }
}

public class TestLoggerProvider : ILoggerProvider
{
    public TestLogger Logger { get; } = new TestLogger();

    public ILogger CreateLogger(string categoryName) => Logger;

    public void Dispose() { }
}

public class TestLogger : ILogger
{
    public int QueryCompilationCount { get; private set; }
    public int QueryExecutionCount { get; private set; }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null!;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (eventId == CoreEventId.QueryCompilationStarting)
        {
            QueryCompilationCount++;
        }
        else if (eventId == RelationalEventId.CommandExecuted)
        {
            QueryExecutionCount++;
        }
    }

    internal void ResetCounts()
    {
        QueryCompilationCount = 0;
        QueryExecutionCount = 0;
    }
}
