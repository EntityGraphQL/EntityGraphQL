using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace Benchmarks
{
    /// <summary>
    /// </summary>
    [ShortRunJob]
    public class CacheBenchmarks : BaseBenchmark
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
        [Benchmark]
        public void FirstStageCompile()
        {
            var graphQLCompiler = new GraphQLCompiler(Schema);
            var gql = new QueryRequest
            {
                Query = query
            };

            for (int i = 0; i < 10000; i++)
            {
                graphQLCompiler.Compile(gql, new QueryRequestContext(null, null));
            }
        }

        [Benchmark]
        public void HashAndLookup()
        {
            var queryCache = new QueryCache();
            queryCache.AddCompiledQuery(query, new GraphQLDocument((name) => name));

            for (int i = 0; i < 10000; i++)
            {
                queryCache.GetCompiledQuery(query, null);
            }
        }
    }
}