using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class ConcurrencyTests
{
    [Fact]
    public void TestFieldConcurrencyLimitExtension()
    {
        var numMovies = 10;
        var schema = SchemaBuilder.FromObject<TestMovieContext>();
        var data = new TestMovieContext
        {
            // Create multiple movies to trigger multiple concurrent operations
            Movies = Enumerable.Range(0, numMovies).Select(i => new Movie { Id = Guid.NewGuid(), Name = $"Movie {i}" }).ToList(),
        };

        // Add field with concurrency limit and use TimedSlowService to track concurrency
        var timedService = new TimedSlowService();
        var field = schema.Type<Movie>().AddField("slowOperation", "Slow operation").ResolveAsync<TimedSlowService>((movie, service) => service.DoTimedWorkAsync(movie.Id), maxConcurrency: 2);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(timedService);

        var query = new QueryRequest { Query = "{ movies { name slowOperation } }" };

        var result = schema.ExecuteRequestWithContext(query, data, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(result.Errors);

        // Verify all operations completed
        dynamic moviesResult = result.Data!["movies"]!;
        Assert.Equal(numMovies, Enumerable.Count(moviesResult));

        // Verify each movie has the expected result
        for (int i = 0; i < numMovies; i++)
        {
            Assert.Equal($"Movie {i}", moviesResult[i].name);
            Assert.StartsWith("Timed result", moviesResult[i].slowOperation);
        }

        // Most importantly: Verify max concurrent operations never exceeded 2
        Assert.True(timedService.MaxConcurrentOperations <= 2, $"Expected max 2 concurrent operations, but saw {timedService.MaxConcurrentOperations}");

        // Also verify that we actually had some concurrency (more than 1)
        Assert.True(timedService.MaxConcurrentOperations > 1, $"Expected some concurrency, but max was only {timedService.MaxConcurrentOperations}");
    }

    [Fact]
    public void TestServiceConcurrencyLimit()
    {
        var numMovies = 10;
        var schema = SchemaBuilder.FromObject<TestMovieContext>();
        var data = new TestMovieContext
        {
            // Create multiple movies to trigger multiple concurrent operations
            Movies = Enumerable.Range(0, numMovies).Select(i => new Movie { Id = Guid.NewGuid(), Name = $"Movie {i}" }).ToList(),
        };

        // Add field that uses the service extension constructor (without field-specific limit)
        var timedService = new TimedSlowService();
        var field = schema.Type<Movie>().AddField("slowOperation", "Slow operation");

        // Set up the field resolver
        field.ResolveAsync<TimedSlowService>((movie, service) => service.DoTimedWorkAsync(movie.Id));

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(timedService);

        // Create execution options with service-level concurrency limit
        var executionOptions = new ExecutionOptions { ServiceConcurrencyLimits = { [typeof(TimedSlowService)] = 3 } };

        var query = new QueryRequest { Query = "{ movies { name slowOperation } }" };

        var result = schema.ExecuteRequestWithContext(query, data, serviceCollection.BuildServiceProvider(), null, executionOptions);
        Assert.Null(result.Errors);

        // Verify all operations completed
        dynamic moviesResult = result.Data!["movies"]!;
        Assert.Equal(numMovies, Enumerable.Count(moviesResult));

        // Verify each movie has the expected result
        for (int i = 0; i < numMovies; i++)
        {
            Assert.Equal($"Movie {i}", moviesResult[i].name);
            Assert.StartsWith("Timed result", moviesResult[i].slowOperation);
        }

        // Most importantly: Verify max concurrent operations never exceeded 3 (the service limit)
        Assert.True(timedService.MaxConcurrentOperations <= 3, $"Expected max 3 concurrent operations, but saw {timedService.MaxConcurrentOperations}");

        // Also verify that we actually had some concurrency (more than 1)
        Assert.True(timedService.MaxConcurrentOperations > 1, $"Expected some concurrency, but max was only {timedService.MaxConcurrentOperations}");
    }

    [Fact]
    public void TestMaxQueryConcurrencyViaExecutionOptions()
    {
        var numMovies = 10;
        var schema = SchemaBuilder.FromObject<TestMovieContext>();
        var data = new TestMovieContext
        {
            // Create multiple movies to trigger multiple concurrent operations
            Movies = Enumerable.Range(0, numMovies).Select(i => new Movie { Id = Guid.NewGuid(), Name = $"Movie {i}" }).ToList(),
        };

        // Add field that uses global concurrency extension (no specific service or field limits)
        var timedService = new TimedSlowService();
        var field = schema.Type<Movie>().AddField("slowOperation", "Slow operation");

        // Set up the field resolver
        field.ResolveAsync<TimedSlowService>((movie, service) => service.DoTimedWorkAsync(movie.Id));

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(timedService);

        // Create execution options with global query-level concurrency limit
        var executionOptions = new ExecutionOptions { MaxQueryConcurrency = 4 };

        var query = new QueryRequest { Query = "{ movies { name slowOperation } }" };

        var result = schema.ExecuteRequestWithContext(query, data, serviceCollection.BuildServiceProvider(), null, executionOptions);
        Assert.Null(result.Errors);

        // Verify all operations completed
        dynamic moviesResult = result.Data!["movies"]!;
        Assert.Equal(numMovies, Enumerable.Count(moviesResult));

        // Verify each movie has the expected result
        for (int i = 0; i < numMovies; i++)
        {
            Assert.Equal($"Movie {i}", moviesResult[i].name);
            Assert.StartsWith("Timed result", moviesResult[i].slowOperation);
        }

        // Most importantly: Verify max concurrent operations never exceeded 4 (the global query limit)
        Assert.True(timedService.MaxConcurrentOperations <= 4, $"Expected max 4 concurrent operations, but saw {timedService.MaxConcurrentOperations}");

        // Also verify that we actually had some concurrency (more than 1)
        Assert.True(timedService.MaxConcurrentOperations > 1, $"Expected some concurrency, but max was only {timedService.MaxConcurrentOperations}");
    }

    [Theory]
    [InlineData(2, 5, 8, 2, "query")]
    [InlineData(10, 3, 7, 3, "service")]
    [InlineData(8, 6, 4, 4, "field")]
    private void TestConcurrencyScenario(int queryLimit, int serviceLimit, int fieldLimit, int expectedMax, string mostRestrictiveLevel)
    {
        var numMovies = 12; // More than any limit to ensure we hit the restrictions
        var schema = SchemaBuilder.FromObject<TestMovieContext>();
        var data = new TestMovieContext { Movies = Enumerable.Range(0, numMovies).Select(i => new Movie { Id = Guid.NewGuid(), Name = $"Movie {i}" }).ToList() };

        var timedService = new TimedSlowService();
        var field = schema.Type<Movie>().AddField("slowOperation", "Slow operation");

        field.ResolveAsync<TimedSlowService>((movie, service) => service.DoTimedWorkAsync(movie.Id), maxConcurrency: fieldLimit);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(timedService);

        var executionOptions = new ExecutionOptions { MaxQueryConcurrency = queryLimit, ServiceConcurrencyLimits = { [typeof(TimedSlowService)] = serviceLimit } };

        var query = new QueryRequest { Query = "{ movies { name slowOperation } }" };
        var result = schema.ExecuteRequestWithContext(query, data, serviceCollection.BuildServiceProvider(), null, executionOptions);

        Assert.Null(result.Errors);

        // Verify the most restrictive limit was enforced
        Assert.True(
            timedService.MaxConcurrentOperations == expectedMax,
            $"Expected max {expectedMax} concurrent operations ({mostRestrictiveLevel} limit), but saw {timedService.MaxConcurrentOperations}"
        );

        // Reset for next test
        timedService.Reset();
    }

    [Fact]
    public void TestConcurrencyLimiterRegistry()
    {
        var concurrencyLimiterRegistry = new ConcurrencyLimiterRegistry();
        // Test the semaphore registry
        concurrencyLimiterRegistry.ClearAllSemaphores();

        var semaphore1 = concurrencyLimiterRegistry.GetSemaphore("test1", 5);
        var semaphore2 = concurrencyLimiterRegistry.GetSemaphore("test1", 5); // Same key
        var semaphore3 = concurrencyLimiterRegistry.GetSemaphore("test2", 3); // Different key

        // Same key should return same semaphore
        Assert.Same(semaphore1, semaphore2);
        Assert.NotSame(semaphore1, semaphore3);

        // Test cleanup
        concurrencyLimiterRegistry.ClearAllSemaphores();
        var semaphore4 = concurrencyLimiterRegistry.GetSemaphore("test1", 5);
        Assert.NotSame(semaphore1, semaphore4); // Should be new after cleanup
    }

    [Fact]
    public void TestConcurrencyLimiterRegistryRequestCleanup()
    {
        var concurrencyLimiterRegistry = new ConcurrencyLimiterRegistry();
        concurrencyLimiterRegistry.ClearAllSemaphores();

        // Create field and query semaphores (should be cleaned)
        var fieldSemaphore = concurrencyLimiterRegistry.GetSemaphore("field_test_5", 5);
        var querySemaphore = concurrencyLimiterRegistry.GetSemaphore("query_test", 10);

        // Create service semaphore (should not be cleaned)
        var serviceSemaphore = concurrencyLimiterRegistry.GetSemaphore("service_TestService", 3);

        concurrencyLimiterRegistry.ClearRequestSemaphores();

        // Field and query semaphores should be gone, service should remain
        var newFieldSemaphore = concurrencyLimiterRegistry.GetSemaphore("field_test_5", 5);
        var newQuerySemaphore = concurrencyLimiterRegistry.GetSemaphore("query_test", 10);
        var sameServiceSemaphore = concurrencyLimiterRegistry.GetSemaphore("service_TestService", 3);

        Assert.NotSame(fieldSemaphore, newFieldSemaphore);
        Assert.NotSame(querySemaphore, newQuerySemaphore);
        Assert.Same(serviceSemaphore, sameServiceSemaphore);

        concurrencyLimiterRegistry.ClearAllSemaphores();
    }

    public class TestMovieContext
    {
        public List<Movie> Movies { get; set; } = new();
    }

    public class Movie
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class SlowService
    {
        public async Task<string> DoSlowWorkAsync(Guid movieId)
        {
            await System.Threading.Tasks.Task.Delay(50); // Small delay for testing
            return $"Slow result for {movieId}";
        }
    }
}

public class TimedSlowService
{
    private int _currentConcurrentOperations = 0;
    public int MaxConcurrentOperations { get; private set; } = 0;

    public async Task<string> DoTimedWorkAsync(Guid movieId)
    {
        var current = Interlocked.Increment(ref _currentConcurrentOperations);
        MaxConcurrentOperations = Math.Max(MaxConcurrentOperations, current);

        try
        {
            await System.Threading.Tasks.Task.Delay(100); // 100ms delay
            return $"Timed result for {movieId}";
        }
        finally
        {
            Interlocked.Decrement(ref _currentConcurrentOperations);
        }
    }

    public void Reset()
    {
        _currentConcurrentOperations = 0;
        MaxConcurrentOperations = 0;
    }
}
