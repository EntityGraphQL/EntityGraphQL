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
    /// |  FirstStageCompile |  32.05 us | 0.179 us | 0.159 us | 11.9629 |     25 KB |
    /// | SecondStageCompile | 127.41 us | 0.554 us | 0.519 us | 28.3203 |     58 KB |
    ///                        159.46                                          82
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