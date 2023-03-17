using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace Benchmarks
{
    /// <summary>
    /// Was testing the difference between always doing the first stage compile vs. a hash and look up for caching purposes
    /// 
    /// Hash and look up is quicker of course
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
        private readonly GraphQLCompiler graphQLCompiler;
        private readonly QueryRequest gql;
        private readonly QueryCache queryCache;

        public CacheBenchmarks()
        {
            graphQLCompiler = new GraphQLCompiler(Schema);
            gql = new QueryRequest
            {
                Query = query
            };

            queryCache = new QueryCache();
            queryCache.AddCompiledQuery(query, new GraphQLDocument(Schema));
        }

        [Benchmark]
        public void FirstStageCompile()
        {
            graphQLCompiler.Compile(gql, new QueryRequestContext(null, null));
        }

        [Benchmark]
        public void HashAndLookup()
        {
            queryCache.GetCompiledQuery(query, null);
        }
    }
}