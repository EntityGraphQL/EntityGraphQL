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
    /// </summary>
    [ShortRunJob]
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
            for (int i = 0; i < 10000; i++)
            {
                graphQLCompiler.Compile(gql, new QueryRequestContext(null, null));
            }
        }

        [Benchmark]
        public void SecondStageCompile()
        {
            for (int i = 0; i < 10000; i++)
            {
                compiledDocument.ExecuteQuery(GetContext(), null, null, null, new ExecutionOptions { NoExecution = true });
            }
        }
    }
}