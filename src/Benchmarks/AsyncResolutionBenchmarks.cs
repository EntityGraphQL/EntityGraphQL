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
/// Was testing a MaxDegreeOfParallelism at the resolving Task<T> level but it didn't really make a difference so removed it.
/// Although it is not the best test as we're not hitting resources like HTTP requests or DB connections.
///
/// Have introduced limiting to control how many async tasks start
///
/// BenchmarkDotNet v0.15.2, macOS 26.0 (25A5338b) [Darwin 25.0.0]
/// Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
/// .NET SDK 9.0.301
///
/// | Method                                  | Mean        | Error    | StdDev   | Gen0      | Gen1    | Allocated  |
/// |---------------------------------------- |------------:|---------:|---------:|----------:|--------:|-----------:|
/// | SmallCollection_WithAsyncFields         |    296.1 us |  2.26 us |  2.22 us |   18.5547 |  3.9063 |  118.81 KB |
/// | LargeCollection_WithAsyncFields         | 13,304.9 us | 35.31 us | 31.30 us | 1015.6250 |       - | 6251.58 KB |
/// | LargeCollection_WithAsyncFields_Limited | 13,305.3 us | 38.34 us | 33.98 us | 1015.6250 | 15.6250 |  6264.8 KB |
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

    private readonly string largeCollectionQueryLimited =
        @"{
            moviesBig {
                id 
                name
                asyncRatingLimit
                asyncDescriptionLimit
                actors {
                    id
                    firstName
                    asyncBioLimit
                    asyncAwardsLimit
                }
            }
        }";

    private readonly QueryRequest smallGql;
    private readonly QueryRequest largeGql;
    private readonly QueryRequest largeGqlLimit;
    private readonly BenchmarkContext context;

    public AsyncResolutionBenchmarks()
    {
        smallGql = new QueryRequest { Query = smallCollectionQuery };
        largeGql = new QueryRequest { Query = largeCollectionQuery };
        largeGqlLimit = new QueryRequest { Query = largeCollectionQueryLimited };
        context = GetContext();
    }

    [GlobalSetup]
    public void Setup()
    {
        // Add async fields to Movie
        Schema.Type<Movie>().AddField("asyncRating", "Async rating").ResolveAsync<FakeServices>((movie, srv) => srv.GetAsyncRatingAsync(movie.Id));
        Schema.Type<Movie>().AddField("asyncDescription", "Async description").ResolveAsync<FakeServices>((movie, srv) => srv.GetAsyncDescriptionAsync(movie.Id));
        Schema.Type<Movie>().AddField("asyncRatingLimit", "Async rating").ResolveAsync<FakeServices>((movie, srv) => srv.GetAsyncRatingAsync(movie.Id), 5);
        Schema.Type<Movie>().AddField("asyncDescriptionLimit", "Async description").ResolveAsync<FakeServices>((movie, srv) => srv.GetAsyncDescriptionAsync(movie.Id), 5);

        // Add async fields to Person (actors)
        Schema.Type<Person>().AddField("asyncBio", "Async bio").ResolveAsync<FakeServices>((person, srv) => srv.GetAsyncBioAsync(person.Id));
        Schema.Type<Person>().AddField("asyncAwards", "Async awards").ResolveAsync<FakeServices>((person, srv) => srv.GetAsyncAwardsAsync(person.Id));
        Schema.Type<Person>().AddField("asyncBioLimit", "Async bio").ResolveAsync<FakeServices>((person, srv) => srv.GetAsyncBioAsync(person.Id), 5);
        Schema.Type<Person>().AddField("asyncAwardsLimit", "Async awards").ResolveAsync<FakeServices>((person, srv) => srv.GetAsyncAwardsAsync(person.Id), 5);

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

    [Benchmark]
    public async Task<object> LargeCollection_WithAsyncFields_Limited()
    {
        // Test with concurrency limit
        var result = await Schema.ExecuteRequestWithContextAsync(largeGqlLimit, context, null, null, new ExecutionOptions { EnableQueryCache = false });
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
