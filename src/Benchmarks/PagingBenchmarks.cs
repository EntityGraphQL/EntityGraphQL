using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Schema.FieldExtensions;

namespace Benchmarks
{
    [SimpleJob(launchCount: 2, warmupCount: 1, targetCount: 5)]
    public class PagingBenchmarks : BaseBenchmark
    {
        [Benchmark]
        public void ConnectionPaging()
        {
            Schema.ReplaceField("movies",
                ctx => ctx.Movies.OrderBy(i => i.Id),
                "Movies"
            )
            .UseConnectionPaging();

            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movies(last: 3 before: ""NA=="") {
                        edges {
                            node {
                                name id
                            }
                            cursor
                        }
                        pageInfo {
                            startCursor
                            endCursor
                            hasNextPage
                            hasPreviousPage
                        }
                        totalCount
                    }
                }"
            });
        }

        [Benchmark]
        public void OffsetPaging()
        {
            Schema.ReplaceField("movies",
                ctx => ctx.Movies.OrderBy(i => i.Id),
                "Movies"
            )
            .UseOffsetPaging();

            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"actorsOffset(skip: 1 take: 1) {
                    items {
                        name id
                    }
                    totalItems
                    hasNextPage
                    hasPreviousPage
                }"
            });
        }
    }
}