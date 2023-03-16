using BenchmarkDotNet.Attributes;
using EntityGraphQL.Compiler.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class ParameterReplacerBenchmarks : BaseBenchmark
    {
        public class Args
        {
            public string? Name { get; set; }
            public float? RatingMin { get; set; }
            public float? RatingMax { get; set; }
            public DateTime? ReleasedBefore { get; set; }
            public DateTime? ReleasedAfter { get; set; }
            public Guid? DirectorId { get; set; }
            public string? DirectorName { get; set; }
            public Guid? ActorId { get; set; }
            public string? ActorName { get; set; }
            public string[] Genres { get; set; } = Array.Empty<string>();
        }

        readonly Expression<Func<BenchmarkContext, IEnumerable<Movie>>> _node;
        readonly Expression<Func<BenchmarkContext, IEnumerable<Movie>>> _node2;
        readonly Expression<Func<BenchmarkContext, Args, IEnumerable<Movie>>> _node3;
        readonly Expression<Func<BenchmarkContext, Args, IEnumerable<Movie>>> _node4;

        public ParameterReplacerBenchmarks()
        {
            _node = (ctx) => ctx.Movies;

            _node2 = (ctx) => ctx.Movies
                   .Where(x => true)
                   .Where(x => true)
                   .Where(x => true)
                   .Where(x => true)
                   .Where(x => true)
                   .Where(x => true)
                   .Where(x => true)
                   .Where(x => true)
                   .Where(x => true)
                   .Where(x => true);

            _node3 = (ctx, args) => ctx.Movies
                    .WhereWhen(i => i.Name == args.Name, !string.IsNullOrWhiteSpace(args.Name))
                    .WhereWhen(i => i.Director.FirstName == args.DirectorName, !string.IsNullOrWhiteSpace(args.DirectorName))
                    .WhereWhen(i => i.Actors.Any(x => x.FirstName == args.ActorName), !string.IsNullOrWhiteSpace(args.ActorName))
                    .WhereWhen(i => i.Rating > args.RatingMin, args.RatingMin.HasValue)
                    .WhereWhen(i => i.Rating < args.RatingMax, args.RatingMax.HasValue)
                    .WhereWhen(i => i.Released > args.ReleasedAfter, args.ReleasedAfter.HasValue)
                    .WhereWhen(i => i.Released < args.ReleasedBefore, args.ReleasedBefore.HasValue)
                    .WhereWhen(i => i.Name == args.Name, !string.IsNullOrWhiteSpace(args.Name))
                    .WhereWhen(i => args.Genres.Contains(i.Genre.Name), args.Genres != null)
                    ;

            _node4 = (ctx, args) => ctx.Set<Movie>()
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
                    ;
        }

        [Benchmark]
        public void PlainDbSet()
        {
            var replacer = new ParameterReplacer();

            var newParam = Expression.Parameter(typeof(BenchmarkContext));

            replacer.Replace(_node, _node.Parameters.First(), newParam);
        }


        [Benchmark]
        public void SetOfBasicWhereStatements()
        {
            var replacer = new ParameterReplacer();

            var newParam = Expression.Parameter(typeof(BenchmarkContext));

            replacer.Replace(_node2, _node2.Parameters.First(), newParam);
        }

        [Benchmark]
        public void SetOfRealisticWhereWhens()
        {
            var replacer = new ParameterReplacer();

            var newParam = Expression.Parameter(typeof(BenchmarkContext));

            replacer.Replace(_node3, _node3.Parameters.First(), newParam);
        }

        [Benchmark]
        public void LargerSetOfWhereWhens()
        {
            var replacer = new ParameterReplacer();

            var newParam = Expression.Parameter(typeof(BenchmarkContext));

            replacer.Replace(_node4, _node4.Parameters.First(), newParam);
        }
    }
}
