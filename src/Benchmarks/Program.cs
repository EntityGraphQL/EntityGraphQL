using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());

        // var bench = new CompileStagesBenchmarks();
        // for (int i = 0; i < 1; i++)
        // {
        //     // bench.FirstStageCompile();
        //     bench.SecondStageCompile();
        // }
    }
}
