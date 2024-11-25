using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Schema;

namespace Benchmarks;

/// <summary>
/// Comparing the speed and allocation of compiling queries with and without caching
/// On a Apple M1 Max 64GB ram
/// Command: `dotnet run -c Debug --framework net8.0` to skip execution
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
///
/// 5.6.0 with net9.0
/// On a Apple M1 Max 64GB ram
/// Command: `dotnet run -c Debug --framework net8.0` to skip execution
///
/// | Method         | Mean     | Error    | StdDev   | Gen0    | Gen1   | Allocated |
/// |--------------- |---------:|---------:|---------:|--------:|-------:|----------:|
/// | CompileNoCache | 97.04 us | 0.795 us | 0.744 us | 15.6250 | 0.9766 |  95.79 KB |
/// | CompileCache   | 80.42 us | 0.190 us | 0.177 us | 12.5732 | 0.4883 |  77.08 KB |
/// </summary>
[MemoryDiagnoser]
public class CompileAllStagesBenchmarks : BaseBenchmark
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

    public CompileAllStagesBenchmarks()
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
