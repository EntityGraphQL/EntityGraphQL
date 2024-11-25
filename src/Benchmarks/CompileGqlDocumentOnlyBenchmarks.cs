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
/// BenchmarkDotNet v0.14.0, macOS Sequoia 15.1 (24B83) [Darwin 24.1.0]
/// Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
/// .NET SDK 9.0.100
///
/// 5.6.0 - HotChocolate.Language 13.9.11
///
/// | Method                                            | Job        | Toolchain              | IterationCount | LaunchCount | WarmupCount | Mean     | Error     | StdDev    |
/// |-------------------------------------------------- |----------- |----------------------- |--------------- |------------ |------------ |---------:|----------:|----------:|
/// | Query_SingleObjectWithArg                         | Job-KXSWTB | InProcessEmitToolchain | Default        | Default     | Default     | 5.065 us | 0.0098 us | 0.0092 us |
/// | Query_SingleObjectWithArg_IncludeSubObject        | Job-KXSWTB | InProcessEmitToolchain | Default        | Default     | Default     | 6.470 us | 0.0129 us | 0.0108 us |
/// | Query_SingleObjectWithArg_IncludeSubObjectAndList | Job-KXSWTB | InProcessEmitToolchain | Default        | Default     | Default     | 7.895 us | 0.0152 us | 0.0127 us |
/// | Query_List                                        | Job-KXSWTB | InProcessEmitToolchain | Default        | Default     | Default     | 2.462 us | 0.0069 us | 0.0061 us |
/// | Query_ListWithTakeArg                             | Job-KXSWTB | InProcessEmitToolchain | Default        | Default     | Default     | 8.604 us | 0.0080 us | 0.0075 us |
/// | Query_SingleObjectWithArg                         | ShortRun   | Default                | 3              | 1           | 3           | 4.910 us | 0.3644 us | 0.0200 us |
/// | Query_SingleObjectWithArg_IncludeSubObject        | ShortRun   | Default                | 3              | 1           | 3           | 6.357 us | 0.2872 us | 0.0157 us |
/// | Query_SingleObjectWithArg_IncludeSubObjectAndList | ShortRun   | Default                | 3              | 1           | 3           | 7.760 us | 0.0774 us | 0.0042 us |
/// | Query_List                                        | ShortRun   | Default                | 3              | 1           | 3           | 2.547 us | 0.0216 us | 0.0012 us |
/// | Query_ListWithTakeArg                             | ShortRun   | Default                | 3              | 1           | 3           | 8.600 us | 1.3116 us | 0.0719 us |
/// </summary>
[ShortRunJob]
public class CompileGqlDocumentOnlyBenchmarks : BaseBenchmark
{
    [Benchmark]
    public void Query_SingleObjectWithArg()
    {
        new GraphQLCompiler(Schema).Compile(
            new QueryRequest
            {
                Query =
                    @"{
                        movie(id: ""433f8132-a7a5-40c9-96c2-e2122fb72e68"") {
                            id name released
                        }
                    }",
            }
        );
    }

    [Benchmark]
    public void Query_SingleObjectWithArg_IncludeSubObject()
    {
        new GraphQLCompiler(Schema).Compile(
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
            }
        );
    }

    [Benchmark]
    public void Query_SingleObjectWithArg_IncludeSubObjectAndList()
    {
        new GraphQLCompiler(Schema).Compile(
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
            }
        );
    }

    [Benchmark]
    public void Query_List()
    {
        new GraphQLCompiler(Schema).Compile(
            new QueryRequest
            {
                Query =
                    @"{
                    movies {
                        id name released
                    }
                }",
            }
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
        new GraphQLCompiler(Schema).Compile(
            new QueryRequest
            {
                Query =
                    @"{
                    movies(take: 10) {
                        id name released
                    }
                }",
            }
        );
    }
}
