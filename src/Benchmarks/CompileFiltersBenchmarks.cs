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
/// 4.2.0
/// |                    Method |        Mean |     Error |    StdDev |    Gen 0 | Allocated |
/// |-------------------------- |------------:|----------:|----------:|---------:|----------:|
/// |                PlainDbSet |    41.61 us |  0.326 us |  0.305 us |   9.5825 |     20 KB |
/// | SetOfBasicWhereStatements |   434.38 us |  3.326 us |  2.777 us |  77.6367 |    159 KB |
/// |     LargerSetOfWhereWhens | 4,982.14 us | 17.287 us | 14.435 us | 664.0625 |  1,377 KB |
///
/// 5.3.0
/// |                    Method |     Mean |     Error |    StdDev |    Gen 0 | Allocated |
/// |-------------------------- |------------:|---------:|---------:|---------:|----------:|
/// |                PlainDbSet |    20.73 us | 0.079 us | 0.066 us |   7.9346 |     16 KB |
/// | SetOfBasicWhereStatements |   158.70 us | 0.547 us | 0.511 us |  41.5039 |     85 KB |
/// |     LargerSetOfWhereWhens | 2,515.82 us | 5.926 us | 5.543 us | 457.0313 |    934 KB |
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
