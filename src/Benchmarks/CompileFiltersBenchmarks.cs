using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using Microsoft.EntityFrameworkCore;

namespace Benchmarks;

/// <summary>
/// On a Apple M1 Max 64GB ram
/// Command: `dotnet run -c Debug`
/// To not actually execute and skip EF
///
/// 5.6.0 with net9.0
///
/// | Method                    | Mean        | Error    | StdDev   | Gen0     | Gen1    | Allocated |
/// |-------------------------- |------------:|---------:|---------:|---------:|--------:|----------:|
/// | PlainDbSet                |    18.98 us | 0.091 us | 0.200 us |   3.2959 |       - |  20.83 KB |
/// | SetOfBasicWhereStatements |    70.65 us | 0.208 us | 0.194 us |  11.9629 |  0.3662 |  73.73 KB |
/// | LargerSetOfWhereWhens     | 1,055.51 us | 1.878 us | 1.757 us | 123.0469 | 23.4375 | 765.25 KB |
///
/// 5.8.0
/// BenchmarkDotNet v0.15.2, macOS 26.0 (25A5338b) [Darwin 25.0.0]
/// Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
/// .NET SDK 9.0.301
///
/// | Method                    | Mean       | Error    | StdDev    | Gen0     | Gen1    | Allocated  |
/// |-------------------------- |-----------:|---------:|----------:|---------:|--------:|-----------:|
/// | PlainDbSet                |   972.5 us |  9.54 us |  12.74 us | 109.3750 | 15.6250 |  671.66 KB |
/// | SetOfBasicWhereStatements | 1,462.2 us | 27.33 us |  51.99 us | 117.1875 | 15.6250 |  740.14 KB |
/// | LargerSetOfWhereWhens     | 3,910.9 us | 65.65 us | 109.69 us | 234.3750 | 62.5000 | 1489.08 KB |
///
/// </summary>
[MemoryDiagnoser]
public class CompileFiltersBenchmarks : BaseBenchmark
{
    private readonly string query =
        @"{
                movies {
                    id name released                        
                }
            }";

    private readonly QueryRequest gql;
    private readonly BenchmarkContext context;

    public CompileFiltersBenchmarks()
    {
        gql = new QueryRequest { Query = query };
        context = GetContext();
    }

    [GlobalSetup(Target = nameof(PlainDbSet))]
    public void SetupPlainDbSet()
    {
        Schema.Query().ReplaceField("movies", (ctx) => ctx.Movies, "List of movies");
    }

    [Benchmark]
    public void PlainDbSet()
    {
        Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true });
    }

    [GlobalSetup(Target = nameof(SetOfBasicWhereStatements))]
    public void SetupSetOfBasicWhereStatements()
    {
        Schema
            .Query()
            .ReplaceField(
                "movies",
                (ctx) =>
                    ctx
                        .Movies.Where(x => true)
                        .Where(x => true)
                        .Where(x => true)
                        .Where(x => true)
                        .Where(x => true)
                        .Where(x => true)
                        .Where(x => true)
                        .Where(x => true)
                        .Where(x => true)
                        .Where(x => true),
                "List of movies"
            );
    }

    [Benchmark]
    public void SetOfBasicWhereStatements()
    {
        Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true });
    }

    [GlobalSetup(Target = nameof(LargerSetOfWhereWhens))]
    public void SetupLargerSetOfWhereWhens()
    {
        Schema
            .Query()
            .ReplaceField(
                "movies",
                new
                {
                    Name = (string?)null,
                    RatingMin = (float?)null,
                    RatingMax = (float?)null,
                    ReleasedBefore = (DateTime?)null,
                    ReleasedAfter = (DateTime?)null,
                    DirectorId = (Guid?)null,
                    DirectorName = (string?)null,
                    ActorId = (Guid?)null,
                    ActorName = (string?)null,
                    Genres = Array.Empty<string>(),
                },
                (ctx, args) =>
                    ctx.Set<Movie>()
                        .AsSplitQuery()
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .WhereWhen(i => i.Name == args.Name, !string.IsNullOrWhiteSpace(args.Name))
                        .WhereWhen(i => i.Director.FirstName == args.DirectorName, !string.IsNullOrWhiteSpace(args.DirectorName))
                        .WhereWhen(i => i.Director.Id == args.DirectorId, args.DirectorId.HasValue)
                        .WhereWhen(i => i.Actors.Any(x => x.Id == args.ActorId), !string.IsNullOrWhiteSpace(args.ActorName))
                        .WhereWhen(i => i.Actors.Any(x => x.FirstName == args.ActorName), args.ActorId.HasValue)
                        .WhereWhen(i => i.Rating > args.RatingMin, args.RatingMin.HasValue)
                        .WhereWhen(i => i.Rating < args.RatingMax, args.RatingMax.HasValue)
                        .WhereWhen(i => i.Released > args.ReleasedAfter, args.ReleasedAfter.HasValue)
                        .WhereWhen(i => i.Released < args.ReleasedBefore, args.ReleasedBefore.HasValue)
                        .WhereWhen(i => args.Genres.Contains(i.Genre.Name), args.Genres.Length > 0)
                        .WhereWhen(i => i.Name == args.Name, !string.IsNullOrWhiteSpace(args.Name))
                        .WhereWhen(i => i.Director.FirstName == args.DirectorName, !string.IsNullOrWhiteSpace(args.DirectorName))
                        .WhereWhen(i => i.Director.Id == args.DirectorId, args.DirectorId.HasValue)
                        .WhereWhen(i => i.Actors.Any(x => x.Id == args.ActorId), !string.IsNullOrWhiteSpace(args.ActorName))
                        .WhereWhen(i => i.Actors.Any(x => x.FirstName == args.ActorName), args.ActorId.HasValue)
                        .WhereWhen(i => i.Rating > args.RatingMin, args.RatingMin.HasValue)
                        .WhereWhen(i => i.Rating < args.RatingMax, args.RatingMax.HasValue)
                        .WhereWhen(i => i.Released > args.ReleasedAfter, args.ReleasedAfter.HasValue)
                        .WhereWhen(i => i.Released < args.ReleasedBefore, args.ReleasedBefore.HasValue)
                        .WhereWhen(i => args.Genres.Contains(i.Genre.Name), args.Genres.Length > 0),
                "List of movies"
            );
    }

    [Benchmark]
    public void LargerSetOfWhereWhens()
    {
        Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true });
    }
}
