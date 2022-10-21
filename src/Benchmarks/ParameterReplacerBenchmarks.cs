using BenchmarkDotNet.Attributes;
using EntityGraphQL.Compiler.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class ParameterReplacerBenchmarks : BaseBenchmark
    {
        Expression<Func<BenchmarkContext, IEnumerable<Movie>>> _node;
            Expression<Func<BenchmarkContext, IEnumerable<Movie>>> _node2;

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
    }
}
