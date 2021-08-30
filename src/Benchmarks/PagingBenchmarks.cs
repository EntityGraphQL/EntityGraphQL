using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;

namespace Benchmarks
{
    [SimpleJob(launchCount: 2, warmupCount: 1, targetCount: 5)]
    public class PagingBenchmarks : BaseBenchmark
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
            Schema.AddField("moviesTakeSkip",
                new
                {
                    take = (int?)null,
                    skip = (int?)null,
                },
                (ctx, args) => ctx.Movies.OrderBy(i => i.Id).Skip(args.skip).Take(args.take),
                "Movies"
            );

            Schema.AddField("moviesConnection",
                ctx => ctx.Movies.OrderBy(i => i.Id),
                "Movies"
            )
            .UseConnectionPaging();

            Schema.ReplaceField("moviesOffset",
                ctx => ctx.Movies.OrderBy(i => i.Id),
                "Movies"
            )
            .UseOffsetPaging();
        }

        [Benchmark]
        public void NoExtension()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    moviesTakeSkip(skip: 1 take: 1) {
                        name id
                    }
                }"
            });
        }
        [Benchmark]
        public void ConnectionPaging()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    moviesConnection(last: 3 before: ""NA=="") {
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
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    moviesOffset(skip: 1 take: 1) {
                        items {
                            name id
                        }
                        totalItems
                        hasNextPage
                        hasPreviousPage
                    }
                }"
            });
        }
    }
}