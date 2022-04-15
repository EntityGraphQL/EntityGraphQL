using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace Benchmarks
{
    /// <summary>
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
            compiledDocument = graphQLCompiler.Compile(new QueryRequestContext(gql, null, null));
        }

        [Benchmark]
        public void FirstStageCompile()
        {
            for (int i = 0; i < 10000; i++)
            {
                graphQLCompiler.Compile(new QueryRequestContext(gql, null, null));
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