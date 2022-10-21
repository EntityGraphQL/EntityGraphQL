using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Schema;
using System.Linq;

namespace Benchmarks
{    
    [MemoryDiagnoser]
    public class CompileFiltersBenchmarks : BaseBenchmark
    {
        private readonly string query = @"{
                movies {
                    id name released                        
                }
            }";

        private readonly QueryRequest gql;
        private readonly BenchmarkContext context;

        public CompileFiltersBenchmarks()
        {
            gql = new QueryRequest
            {
                Query = query
            };
            context = GetContext();
        }

        [GlobalSetup(Target = nameof(CompileNoWhere))]
        public void SetupCompileNoWhere()
        {
            Schema.Query().ReplaceField(
              "movies",
              (ctx) => ctx.Movies,
              "List of movies");
        }

        [Benchmark]
        public void CompileNoWhere()
        {
            Schema.ExecuteRequest(gql, context, null, null, new ExecutionOptions
            {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true
            });
        }


        [GlobalSetup(Target = nameof(CompileLotsOfWhere))]
        public void SetupCompileLotsOfWhere()
        {
            Schema.Query().ReplaceField(
              "movies",
              (ctx) => ctx.Movies
                  .Where(x => true)
                  .Where(x => true)
                  .Where(x => true)
                  .Where(x => true)
                  .Where(x => true)
                  .Where(x => true)
                  .Where(x => true)
                  .Where(x => true)
                  .Where(x => true)
                  .Where(x => true),
              "List of movies");
        }

        [Benchmark]
        public void CompileLotsOfWhere()
        {
            Schema.ExecuteRequest(gql, context, null, null, new ExecutionOptions
            {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true
            });
        }
    }
}