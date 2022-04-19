using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;

namespace Benchmarks
{
    /// <summary>
    /// Testing different ways of paging data
    /// </summary>
    [ShortRunJob]
    public class PagingBenchmarks : BaseBenchmark
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
            Schema.Query().AddField("moviesTakeSkip",
                new
                {
                    take = (int?)null,
                    skip = (int?)null,
                },
                (ctx, args) => ctx.Movies.OrderBy(i => i.Id).Skip(args.skip).Take(args.take),
                "Movies"
            );

            Schema.Query().AddField("moviesConnection",
                ctx => ctx.Movies.OrderBy(i => i.Id),
                "Movies"
            )
            .UseConnectionPaging();

            Schema.Query().ReplaceField("moviesOffset",
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