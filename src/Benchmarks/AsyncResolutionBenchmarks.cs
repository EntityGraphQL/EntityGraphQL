using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace Benchmarks;

/// <summary>
/// Benchmarks for testing different async field resolution strategies
/// Tests the performance impact of concurrent vs sequential async processing
///
/// Was testing a MaxDegreeOfParallelism but it didn't really make a difference so removed it.
/// Although it is not the best test as we're not hitting resources like HTTP requests or DB connections.
/// Users can control better with Lazy<T>.
///
/// BenchmarkDotNet v0.15.2, macOS 26.0 (25A5338b) [Darwin 25.0.0]
/// Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
/// .NET SDK 9.0.301
///
/// | Method                          | MaxDegreeOfParallelism | Mean        | Error     | StdDev    | Gen0      | Gen1     | Allocated  |
/// |-------------------------------- |----------------------- |------------:|----------:|----------:|----------:|---------:|-----------:|
/// | SmallCollection_WithAsyncFields | -1                     |    306.1 us |   2.79 us |   2.74 us |   18.5547 |   3.9063 |  116.33 KB |
/// | SmallCollection_WithAsyncFields | 1                      |    309.1 us |   2.89 us |   2.70 us |   18.5547 |   0.4883 |  116.33 KB |
/// | SmallCollection_WithAsyncFields | 4                      |    316.8 us |   6.28 us |  10.31 us |   18.5547 |   0.4883 |  116.34 KB |
/// | SmallCollection_WithAsyncFields | 8                      |    316.9 us |   6.22 us |   7.40 us |   18.5547 |   0.4883 |  116.34 KB |
/// | SmallCollection_WithAsyncFields | 32                     |    304.7 us |   1.70 us |   1.42 us |   18.5547 |   0.4883 |  116.34 KB |
/// | LargeCollection_WithAsyncFields | -1                     | 13,851.8 us |  73.07 us |  68.35 us | 1015.6250 |        - | 6249.55 KB |
/// | LargeCollection_WithAsyncFields | 1                      | 13,879.4 us |  88.79 us |  83.06 us | 1015.6250 |  15.6250 | 6249.58 KB |
/// | LargeCollection_WithAsyncFields | 4                      | 13,870.4 us | 265.47 us | 248.32 us | 1015.6250 | 421.8750 | 6249.56 KB |
/// | LargeCollection_WithAsyncFields | 8                      | 13,883.8 us | 239.14 us | 223.69 us | 1015.6250 |  15.6250 | 6249.58 KB |
/// | LargeCollection_WithAsyncFields | 32                     | 13,710.5 us |  63.32 us |  59.23 us | 1015.6250 |  15.6250 | 6249.57 KB |
///
/// </summary>
[MemoryDiagnoser]
public class AsyncResolutionBenchmarks : BaseBenchmark
{
    private readonly string smallCollectionQuery =
        @"{
            movies {
                id 
                name
                asyncRating
                asyncDescription
            }
        }";

    private readonly string largeCollectionQuery =
        @"{
            moviesBig {
                id 
                name
                asyncRating
                asyncDescription
                actors {
                    id
                    firstName
                    asyncBio
                    asyncAwards
                }
            }
        }";

    private readonly QueryRequest smallGql;
    private readonly QueryRequest largeGql;
    private readonly BenchmarkContext context;

    public AsyncResolutionBenchmarks()
    {
        smallGql = new QueryRequest { Query = smallCollectionQuery };
        largeGql = new QueryRequest { Query = largeCollectionQuery };
        context = GetContext();
    }

    [GlobalSetup]
    public void Setup()
    {
        // Add async fields to Movie
        Schema.Type<Movie>().AddField("asyncRating", "Async rating").Resolve<FakeServices>((movie, srv) => srv.GetAsyncRatingAsync(movie.Id));
        Schema.Type<Movie>().AddField("asyncDescription", "Async description").Resolve<FakeServices>((movie, srv) => srv.GetAsyncDescriptionAsync(movie.Id));

        // Add async fields to Person (actors)
        Schema.Type<Person>().AddField("asyncBio", "Async bio").Resolve<FakeServices>((person, srv) => srv.GetAsyncBioAsync(person.Id));
        Schema.Type<Person>().AddField("asyncAwards", "Async awards").Resolve<FakeServices>((person, srv) => srv.GetAsyncAwardsAsync(person.Id));

        // Replace movies field to return a controlled set
        Schema
            .Query()
            .ReplaceField(
                "movies",
                (ctx) => ctx.Movies.Take(100), // Small collection for testing
                "List of movies with async fields"
            );

        Schema
            .Query()
            .AddField(
                "moviesBig",
                (ctx) => ctx.Movies.Take(1000), // Larger collection
                "List of movies with async fields"
            );
    }

    [Benchmark]
    public async Task<object> SmallCollection_WithAsyncFields()
    {
        var result = await Schema.ExecuteRequestWithContextAsync(smallGql, context, null, null, new ExecutionOptions { EnableQueryCache = false });
        return result;
    }

    [Benchmark]
    public async Task<object> LargeCollection_WithAsyncFields()
    {
        var result = await Schema.ExecuteRequestWithContextAsync(largeGql, context, null, null, new ExecutionOptions { EnableQueryCache = false });
        return result;
    }

    public class FakeServices
    {
        // Simulate async operations with controlled delays
        public async Task<float> GetAsyncRatingAsync(Guid movieId)
        {
            await Task.Delay(50); // Simulate async work (API call, etc.)
            return 7.5f + movieId.GetHashCode() % 10 * 0.3f;
        }

        public async Task<string> GetAsyncDescriptionAsync(Guid movieId)
        {
            await Task.Delay(75); // Simulate async work
            return $"Async description for movie {movieId}";
        }

        public async Task<string> GetAsyncBioAsync(Guid personId)
        {
            await Task.Delay(100); // Simulate async work
            return $"Async bio for person {personId}";
        }

        public async Task<List<string>> GetAsyncAwardsAsync(Guid personId)
        {
            await Task.Delay(110); // Simulate async work
            return new List<string> { "Best Actor", "Best Supporting" };
        }
    }
}
