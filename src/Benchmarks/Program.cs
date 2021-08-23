using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(
                args,
                new DebugInProcessConfig()
            );
            // var queryBench = new QueryBenchmarks();

            // for (int i = 0; i < 1000; i++)
            // {
            //     Console.WriteLine($"Executing {i} ...");
            //     queryBench.Query_List();
            // }
        }
    }
}
