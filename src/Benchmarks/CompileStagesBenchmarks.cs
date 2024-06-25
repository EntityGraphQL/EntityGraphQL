using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Schema;

namespace Benchmarks;

/// <summary>
/// Comparing the speed and allocation of compiling queries with and without caching
/// On a Apple M1 Max 64GB ram
/// Command: `dotnet run -c Release`
///
/// 4.1.2
/// |         Method |      Mean |    Error |   StdDev |   Gen 0 | Allocated |
/// |--------------- |----------:|---------:|---------:|--------:|----------:|
/// | CompileNoCache | 104.25 us | 0.439 us | 0.389 us | 36.2549 |     74 KB |
/// |   CompileCache |  80.00 us | 0.236 us | 0.221 us | 27.5879 |     57 KB |
///
/// 4.3.1 (4.2 was basically the same)
/// |         Method |     Mean |   Error |  StdDev |   Gen 0 | Allocated |
/// |--------------- |---------:|--------:|--------:|--------:|----------:|
/// | CompileNoCache | 142.4 us | 1.37 us | 1.28 us | 49.8047 |    102 KB |
/// |   CompileCache | 109.3 us | 0.47 us | 0.44 us | 38.8184 |     79 KB |
///
/// 5.2.1 (5.0 & 5.1 were basically the same)
/// |         Method |      Mean |    Error |   StdDev |   Gen 0 | Allocated |
/// |--------------- |----------:|---------:|---------:|--------:|----------:|
/// | CompileNoCache | 116.21 us | 0.399 us | 0.354 us | 42.4805 |     87 KB |
/// |   CompileCache |  95.66 us | 0.388 us | 0.363 us | 33.5693 |     69 KB |
///
/// 5.3.0
/// |         Method |      Mean |    Error |   StdDev |   Gen 0 | Allocated |
/// |--------------- |----------:|---------:|---------:|--------:|----------:|
/// | CompileNoCache | 125.85 us | 0.650 us | 0.543 us | 44.4336 |     91 KB |
/// |   CompileCache |  97.15 us | 0.396 us | 0.371 us | 33.9355 |     69 KB |
/// </summary>
[MemoryDiagnoser]
public class CompileStagesBenchmarks : BaseBenchmark
{
    private readonly string query =
        @"{
                    movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                        id name released
                        director {
                            id name dob
                            directorOf {
                                id name released
                            }
                        }
                        actors {
                            id name dob
                        }
                    }
                }";

    private readonly QueryRequest gql;
    private readonly BenchmarkContext context;

    public CompileStagesBenchmarks()
    {
        gql = new QueryRequest { Query = query };
        context = GetContext();
    }

    [Benchmark]
    public void CompileNoCache()
    {
        Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = false });
    }

    [Benchmark]
    public void CompileCache()
    {
        Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true });
    }
}
