using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Compiler;
using EntityGraphQL.Extensions;

namespace Benchmarks;

/// <summary>
/// Benchmarks to test just the string graphql document to EntityGraphQL IGraphQLNode compilation.
/// Not to the expression that will be executed
///
/// BenchmarkDotNet v0.15.2, macOS 26.0 (25A5338b) [Darwin 25.0.0]
/// Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
/// .NET SDK 9.0.301
/// 5.7.1
/// | Method                                            | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
/// |-------------------------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
/// | Query_SingleObjectWithArg                         | 4.899 us | 0.0515 us | 0.0481 us | 1.5564 |      - |   9.54 KB |
/// | Query_SingleObjectWithArg_IncludeSubObject        | 6.261 us | 0.0081 us | 0.0072 us | 2.0905 | 0.0534 |  12.82 KB |
/// | Query_SingleObjectWithArg_IncludeSubObjectAndList | 7.780 us | 0.0166 us | 0.0139 us | 2.6855 | 0.0916 |  16.47 KB |
/// | Query_List                                        | 2.431 us | 0.0055 us | 0.0046 us | 0.9117 | 0.0076 |   5.59 KB |
/// | Query_ListWithTakeArg                             | 8.482 us | 0.1298 us | 0.1151 us | 2.5177 | 0.0305 |  15.49 KB |
///
/// 6.0.0
/// | Method                                            | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
/// |-------------------------------------------------- |---------:|----------:|----------:|-------:|-------:|----------:|
/// | Query_SingleObjectWithArg                         | 3.897 us | 0.0454 us | 0.0379 us | 1.0376 |      - |    6.5 KB |
/// | Query_SingleObjectWithArg_IncludeSubObject        | 4.452 us | 0.0890 us | 0.1060 us | 1.1902 |      - |   7.39 KB |
/// | Query_SingleObjectWithArg_IncludeSubObjectAndList | 5.113 us | 0.1020 us | 0.1558 us | 1.3733 |      - |    8.5 KB |
/// | Query_List                                        | 1.784 us | 0.0120 us | 0.0113 us | 0.5760 | 0.0019 |   3.54 KB |
/// | Query_ListWithTakeArg                             | 7.991 us | 0.0361 us | 0.0320 us | 2.1515 | 0.0153 |   13.2 KB |
///
/// </summary>
[MemoryDiagnoser]
public class CompileGqlDocumentOnlyBenchmarks : BaseBenchmark
{
    [Benchmark]
    public void Query_SingleObjectWithArg()
    {
        GraphQLParser.Parse(
            new QueryRequest
            {
                Query =
                    @"{
                        movie(id: ""433f8132-a7a5-40c9-96c2-e2122fb72e68"") {
                            id name released
                        }
                    }",
            },
            Schema
        );
    }

    [Benchmark]
    public void Query_SingleObjectWithArg_IncludeSubObject()
    {
        GraphQLParser.Parse(
            new QueryRequest
            {
                Query =
                    @"{
                    movie(id: ""1deb79a1-59b1-4360-8d95-04bd7107ad8c"") {
                        id name released
                        director {
                            id name dob
                        }
                    }
                }",
            },
            Schema
        );
    }

    [Benchmark]
    public void Query_SingleObjectWithArg_IncludeSubObjectAndList()
    {
        GraphQLParser.Parse(
            new QueryRequest
            {
                Query =
                    @"{
                    movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                        id name released
                        director {
                            id name dob
                        }
                        actors {
                            id name dob
                        }
                    }
                }",
            },
            Schema
        );
    }

    [Benchmark]
    public void Query_List()
    {
        GraphQLParser.Parse(
            new QueryRequest
            {
                Query =
                    @"{
                    movies {
                        id name released
                    }
                }",
            },
            Schema
        );
    }

    [GlobalSetup(Target = nameof(Query_ListWithTakeArg))]
    public void ModifyField()
    {
        Schema.Query().ReplaceField("movies", new { take = (int?)null }, (ctx, args) => ctx.Movies.Take(args.take), "List of movies");
    }

    [Benchmark]
    public void Query_ListWithTakeArg()
    {
        GraphQLParser.Parse(
            new QueryRequest
            {
                Query =
                    @"{
                    movies(take: 10) {
                        id name released
                    }
                }",
            },
            Schema
        );
    }
}
