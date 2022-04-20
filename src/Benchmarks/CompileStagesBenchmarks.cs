using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace Benchmarks
{
    /// <summary>
    /// Comparing the different stages of compiling.
    /// Stage 1 - query to Expressions and metadata. This can be cached and reused with different variables
    /// Stage 2 - the stage1 result into a final LambdaExpression that is executed. This may be built twice and executed twice, 
    /// once without service field and then with
    /// 
    /// 1.2.x
    /// |             Method |     Mean |   Error |  StdDev |   Gen 0 | Allocated |
    /// |------------------- |---------:|--------:|--------:|--------:|----------:|
    /// |  FirstStageCompile | 164.9 us | 3.28 us | 4.60 us | 18.0664 |     38 KB |
    /// | SecondStageCompile | 103.7 us | 1.57 us | 1.40 us | 25.1465 |     52 KB |  
    ///                Total | 268.6                                        90
    /// 
    /// 2.0.0
    /// |             Method |      Mean |    Error |   StdDev |   Gen 0 | Allocated |
    /// |------------------- |----------:|---------:|---------:|--------:|----------:|
    /// |  FirstStageCompile |  31.89 us | 0.143 us | 0.134 us | 11.7188 |     24 KB |
    /// | SecondStageCompile | 129.44 us | 0.548 us | 0.513 us | 27.8320 |     57 KB |
    ///                        161.33                                          81
    /// </summary>
    [MemoryDiagnoser]
    public class CompileStagesBenchmarks : BaseBenchmark
    {
        private readonly string query = @"{
                    movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                        id name released
                        director {
                            id name dob
                        }
                        actors {
                            id name dob
                        }
                    }
                }";
        private readonly GraphQLCompiler graphQLCompiler;
        private readonly QueryRequest gql;
        private readonly GraphQLDocument compiledDocument;

        public CompileStagesBenchmarks()
        {
            graphQLCompiler = new GraphQLCompiler(Schema);
            gql = new QueryRequest
            {
                Query = query
            };
            compiledDocument = graphQLCompiler.Compile(gql, new QueryRequestContext(null, null));
        }

        [Benchmark]
        public void FirstStageCompile()
        {
            graphQLCompiler.Compile(gql, new QueryRequestContext(null, null));
        }

        [Benchmark]
        public void SecondStageCompile()
        {
            compiledDocument.ExecuteQuery(GetContext(), null, null, null, new ExecutionOptions { NoExecution = true });
        }
    }
}