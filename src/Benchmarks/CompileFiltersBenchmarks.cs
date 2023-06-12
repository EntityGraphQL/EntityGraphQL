using BenchmarkDotNet.Attributes;
using EntityGraphQL.Compiler.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.EntityFrameworkCore;
using EntityGraphQL.Schema;
using EntityGraphQL;

namespace Benchmarks
{
    /// <summary>
    /// 4.2.0
    /// |-------------------------- |------------:|----------:|----------:|---------:|----------:|
    /// |                PlainDbSet |    41.61 us |  0.326 us |  0.305 us |   9.5825 |     20 KB |
    /// | SetOfBasicWhereStatements |   434.38 us |  3.326 us |  2.777 us |  77.6367 |    159 KB |
    /// |     LargerSetOfWhereWhens | 4,982.14 us | 17.287 us | 14.435 us | 664.0625 |  1,377 KB |
    /// </summary>
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

        [GlobalSetup(Target = nameof(PlainDbSet))]
        public void SetupPlainDbSet()
        {
            Schema.Query().ReplaceField(
              "movies",
              (ctx) => ctx.Movies,
              "List of movies");
        }

        [Benchmark]
        public void PlainDbSet()
        {
            Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions
            {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true
            });
        }


        [GlobalSetup(Target = nameof(SetOfBasicWhereStatements))]
        public void SetupSetOfBasicWhereStatements()
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
        public void SetOfBasicWhereStatements()
        {
            Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions
            {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true
            });
        }


        [GlobalSetup(Target = nameof(LargerSetOfWhereWhens))]
        public void SetupLargerSetOfWhereWhens()
        {
            Schema.Query().ReplaceField(
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
              (ctx, args) => ctx.Set<Movie>()
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
                    .WhereWhen(i => args.Genres.Contains(i.Genre.Name), args.Genres.Length > 0)
              , "List of movies");
        }

        [Benchmark]
        public void LargerSetOfWhereWhens()
        {
            Schema.ExecuteRequestWithContext(gql, context, null, null, new ExecutionOptions
            {
#if DEBUG
                NoExecution = true,
#endif
                EnableQueryCache = true
            });
        }
    }
}