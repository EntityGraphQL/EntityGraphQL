using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// Benchmarks for the async field resolution path using fast (no-I/O) resolvers.
/// Unlike AsyncResolutionBenchmarks which uses Task.Delay and is I/O dominated,
/// these use ValueTask.FromResult so the framework's reflection overhead is measurable.
///
/// They exercise the paths optimized by ExecutableGraphQLStatement's static caches:
///   - Task&lt;T&gt;.Result property lookup (taskResultProperties cache)
///   - ValueTask&lt;T&gt;.AsTask() method lookup (valueTaskAsTaskMethods cache)
///   - Complex object metadata - property/field enumeration (complexObjectMetadataCache)
/// </summary>
[MemoryDiagnoser]
public class FastAsyncResolutionBenchmarks
{
    private readonly SchemaProvider<BenchmarkContext> schema;
    private readonly IServiceProvider services;
    private readonly BenchmarkContext context;
    private readonly QueryRequest smallGql;
    private readonly QueryRequest largeGql;

    public FastAsyncResolutionBenchmarks()
    {
        var sc = new ServiceCollection();
        sc.AddDbContext<BenchmarkContext>();
        sc.AddTransient<FastServices>();

        var builtSchema = SchemaBuilder.FromObject<BenchmarkContext>();
        builtSchema.UpdateType<Person>(t => t.AddField("name", p => $"{p.FirstName} {p.LastName}", "Full name"));
        builtSchema.Query().ReplaceField("movies", ctx => ctx.Movies.Take(100), "First 100 movies");
        builtSchema.Query().AddField("moviesBig", ctx => ctx.Movies.Take(1000), "First 1000 movies");
        builtSchema.Type<Movie>().AddField("asyncRating", "Async rating").ResolveAsync<FastServices, float>((movie, srv) => srv.GetRatingAsync(movie.Id));
        builtSchema.Type<Movie>().AddField("asyncScore", "Async score").ResolveAsync<FastServices, double>((movie, srv) => srv.GetScoreAsync(movie.Rating));
        builtSchema.Type<Person>().AddField("asyncFullName", "Async full name").ResolveAsync<FastServices, string>((person, srv) => srv.GetFullNameAsync(person.FirstName, person.LastName));
        sc.AddSingleton(builtSchema);

        services = sc.BuildServiceProvider();
        schema = services.GetRequiredService<SchemaProvider<BenchmarkContext>>();
        context = services.GetRequiredService<BenchmarkContext>();
        DataLoader.EnsureDbCreated(context);

        smallGql = new QueryRequest { Query = "{ movies { id name asyncRating asyncScore } }" };
        largeGql = new QueryRequest { Query = "{ moviesBig { id name asyncRating actors { id firstName asyncFullName } } }" };
    }

    [Benchmark]
    public async Task<object> SmallCollection_NoCache()
    {
        return await schema.ExecuteRequestWithContextAsync(smallGql, context, services, null, new ExecutionOptions { EnableQueryCache = false });
    }

    [Benchmark]
    public async Task<object> SmallCollection_WithCache()
    {
        return await schema.ExecuteRequestWithContextAsync(smallGql, context, services, null, new ExecutionOptions { EnableQueryCache = true, CacheCompiledDelegates = true });
    }

    [Benchmark]
    public async Task<object> LargeCollection_NoCache()
    {
        return await schema.ExecuteRequestWithContextAsync(largeGql, context, services, null, new ExecutionOptions { EnableQueryCache = false });
    }

    [Benchmark]
    public async Task<object> LargeCollection_WithCache()
    {
        return await schema.ExecuteRequestWithContextAsync(largeGql, context, services, null, new ExecutionOptions { EnableQueryCache = true, CacheCompiledDelegates = true });
    }

    public class FastServices
    {
        public ValueTask<float> GetRatingAsync(Guid id) => ValueTask.FromResult(7.5f);

        public ValueTask<double> GetScoreAsync(float rating) => ValueTask.FromResult((double)rating * 10);

        public ValueTask<string> GetFullNameAsync(string firstName, string lastName) => ValueTask.FromResult($"{firstName} {lastName}");
    }
}
