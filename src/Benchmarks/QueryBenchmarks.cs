using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Extensions;

namespace Benchmarks
{
    [SimpleJob(launchCount: 2, warmupCount: 1, targetCount: 5)]
    public class QueryBenchmarks : BaseBenchmark
    {
        [Benchmark]
        public void Query_SingleObjectWithArg()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movie(id: ""433f8132-a7a5-40c9-96c2-e2122fb72e68"") {
                        id name released
                    }
                }"
            });
        }

        [Benchmark]
        public void Query_SingleObjectWithArg_IncludeSubObject()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movie(id: ""1deb79a1-59b1-4360-8d95-04bd7107ad8c"") {
                        id name released
                        director {
                            id name dob
                        }
                    }
                }"
            });
        }
        [Benchmark]
        public void Query_SingleObjectWithArg_IncludeSubObjectAndList()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                        id name released
                        director {
                            id name dob
                        }
                        actors {
                            id name dob
                        }
                    }
                }"
            });
        }
        [Benchmark]
        public void Query_List()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movies {
                        id name released
                    }
                }"
            });
        }
        [GlobalSetup(Target = nameof(Query_ListWithTakeArg))]
        public void ModifyField()
        {
            Schema.ReplaceField(
                "movies",
                new
                {
                    take = (int?)null
                },
                (ctx, args) => ctx.Movies.Take(args.take),
                "List of movies");
        }
        [Benchmark]
        public void Query_ListWithTakeArg()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movies(take: 10) {
                        id name released
                    }
                }"
            });
        }
    }
}