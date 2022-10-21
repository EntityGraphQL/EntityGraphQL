using BenchmarkDotNet.Attributes;
using EntityGraphQL.Compiler.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;

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
            public string? DirectorName { get; set; }
            public string? ActorName { get; set; }
            public string Genre { get; set; }
        }

        Expression<Func<BenchmarkContext, IEnumerable<Movie>>> _node;
        Expression<Func<BenchmarkContext, IEnumerable<Movie>>> _node2;
        Expression<Func<BenchmarkContext, Args, IEnumerable<Movie>>> _node3;

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
                    .WhereWhen(i => i.Genre.Name == args.Genre, args.Genre != null)
                    ;
        }

        [GlobalSetup(Target = nameof(CompileNoWhere))]
        public void SetupCompileNoWhere()
        {
            
        }

        [Benchmark]
        public void CompileNoWhere()
        {
            var replacer = new ParameterReplacer();

            var newParam = Expression.Parameter(typeof(BenchmarkContext));

            replacer.Replace(_node, _node.Parameters.First(), newParam);
        }


        [GlobalSetup(Target = nameof(CompileLotsOfWhere))]
        public void SetupCompileLotsOfWhere()
        {
           
        }

        [Benchmark]
        public void CompileLotsOfWhere()
        {
            var replacer = new ParameterReplacer();

            var newParam = Expression.Parameter(typeof(BenchmarkContext));

            replacer.Replace(_node2, _node2.Parameters.First(), newParam);
        }

        [Benchmark]
        public void CompileComplicated()
        {
            var replacer = new ParameterReplacer();

            var newParam = Expression.Parameter(typeof(BenchmarkContext));

            replacer.Replace(_node3, _node3.Parameters.First(), newParam);
        }
    }
}
