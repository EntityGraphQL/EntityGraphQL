using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Schema;

namespace Benchmarks
{
    /// <summary>
    /// Comparing the different stages of compiling.
    /// Stage 1 - query to Expressions and metadata. This can be cached and reused with different variables
    /// Stage 2 - the stage1 result into a final LambdaExpression that is executed. This may be built twice and executed twice, 
    /// once without service fields and then with
    /// 
    /// 1.2.x
    /// |             Method |     Mean |   Error |  StdDev |   Gen 0 | Allocated |
    /// |------------------- |---------:|--------:|--------:|--------:|----------:|
    /// |  FirstStageCompile | 164.9 us | 3.28 us | 4.60 us | 18.0664 |     38 KB |
    /// | SecondStageCompile | 103.7 us | 1.57 us | 1.40 us | 25.1465 |     52 KB |  
    ///                Total | 268.6                                        90
    /// 
    /// 2.0.x
    /// |             Method |     Mean |   Error |  StdDev |   Gen 0 | Allocated |
    /// |------------------- |---------:|--------:|--------:|--------:|----------:|
    /// |     CompileNoCache | 187.3 us | 1.78 us | 1.67 us | 38.0859 |     78 KB |
    /// |       CompileCache | 150.3 us | 1.25 us | 1.04 us | 29.5410 |     61 KB |
    /// 
    /// 4.1.0 - added to the query used
    /// |             Method |     Mean |   Error |  StdDev |   Gen 0 | Allocated |
    /// |------------------- |---------:|--------:|--------:|--------:|----------:|
    /// |     CompileNoCache | 192.9 us | 0.94 us | 0.88 us | 36.6211 |     75 KB |
    /// |       CompileCache | 150.4 us | 0.50 us | 0.42 us | 27.8320 |     57 KB |
    /// </summary>
    [MemoryDiagnoser]
    public class CompileStagesBenchmarks : BaseBenchmark
    {
        private readonly string query = @"{
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
            gql = new QueryRequest
            {
                Query = query
            };
            context = GetContext();
        }

        [Benchmark]
        public void CompileNoCache()
        {
            Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions
            {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = false
            });
        }
        [Benchmark]
        public void CompileCache()
        {
            Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions
            {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true
            });
        }
    }
}