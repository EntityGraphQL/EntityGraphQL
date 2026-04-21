using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance).WithOptions(ConfigOptions.DisableOptimizationsValidator);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

        // var bench = new CompileStagesBenchmarks();
        // for (int i = 0; i < 1; i++)
        // {
        //     // bench.FirstStageCompile();
        //     bench.SecondStageCompile();
        // }
    }
}
