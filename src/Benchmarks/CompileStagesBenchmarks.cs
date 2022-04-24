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
    /// |             Method |     Mean |   Error |  StdDev |   Gen 0 | Allocated |
    /// |------------------- |---------:|--------:|--------:|--------:|----------:|
    /// |            Compile | 161.4 us | 1.09 us | 1.02 us | 38.5742 |     79 KB |
    /// 
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

        public CompileStagesBenchmarks()
        {
            graphQLCompiler = new GraphQLCompiler(Schema);
            gql = new QueryRequest
            {
                Query = query
            };
        }

        [Benchmark]
        public void Compile()
        {
            var compiledDocument = graphQLCompiler.Compile(gql, new QueryRequestContext(null, null));
            compiledDocument.ExecuteQuery(GetContext(), null, null, null, new ExecutionOptions { NoExecution = true });
        }
    }
}