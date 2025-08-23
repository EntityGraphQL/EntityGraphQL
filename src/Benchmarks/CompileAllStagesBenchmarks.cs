using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Schema;

namespace Benchmarks;

/// <summary>
/// Comparing the speed and allocation of compiling queries with and without caching
/// On a Apple M1 Max 64GB ram
/// Command: `dotnet run -c Debug --framework net8.0` to skip execution
///
/// 5.6.0 with net9.0
/// On a Apple M1 Max 64GB ram
/// Command: `dotnet run -c Debug --framework net8.0` to skip execution
///
/// | Method         | Mean     | Error    | StdDev   | Gen0    | Gen1   | Allocated |
/// |--------------- |---------:|---------:|---------:|--------:|-------:|----------:|
/// | CompileNoCache | 97.04 us | 0.795 us | 0.744 us | 15.6250 | 0.9766 |  95.79 KB |
/// | CompileCache   | 80.42 us | 0.190 us | 0.177 us | 12.5732 | 0.4883 |  77.08 KB |
///
/// BenchmarkDotNet v0.15.2, macOS 26.0 (25A5338b) [Darwin 25.0.0]
/// Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
/// .NET SDK 9.0.301
/// | Method         | Mean     | Error    | StdDev   | Gen0    | Gen1   | Allocated |
/// |--------------- |---------:|---------:|---------:|--------:|-------:|----------:|
/// | CompileNoCache | 93.56 us | 1.776 us | 1.662 us | 15.3809 | 0.7324 |  94.25 KB |
/// | CompileCache   | 77.24 us | 1.524 us | 1.425 us | 12.2070 | 0.2441 |  75.39 KB |
///
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
