using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Compiler.Util;

namespace Benchmarks
{
    /// <summary>
    /// Was testing the performance of using reflection to build the correct types from variables or using System.Text.Json
    /// 
    /// Reflection wins
    /// |                      Method |        Job |              Toolchain | IterationCount | LaunchCount | WarmupCount |        Mean |        Error |     StdDev |
    /// |---------------------------- |----------- |----------------------- |--------------- |------------ |------------ |------------:|-------------:|-----------:|
    /// | ObjectWithJsonSerialization | Job-DOGPPG | InProcessEmitToolchain |        Default |     Default |     Default | 7,524.18 ms |   146.326 ms | 227.812 ms |
    /// |        ObjectWithReflection | Job-DOGPPG | InProcessEmitToolchain |        Default |     Default |     Default |    21.77 ms |     0.355 ms |   0.332 ms |
    /// |   ListWithJsonSerialization | Job-DOGPPG | InProcessEmitToolchain |        Default |     Default |     Default | 9,686.72 ms |   193.058 ms | 327.827 ms |
    /// |          ListWithReflection | Job-DOGPPG | InProcessEmitToolchain |        Default |     Default |     Default |    44.22 ms |     0.202 ms |   0.189 ms |
    /// | ObjectWithJsonSerialization |   ShortRun |                Default |              3 |           1 |           3 | 7,278.49 ms | 5,676.616 ms | 311.154 ms |
    /// |        ObjectWithReflection |   ShortRun |                Default |              3 |           1 |           3 |    20.41 ms |     1.685 ms |   0.092 ms |
    /// |   ListWithJsonSerialization |   ShortRun |                Default |              3 |           1 |           3 | 9,877.59 ms | 2,168.229 ms | 118.848 ms |
    /// |          ListWithReflection |   ShortRun |                Default |              3 |           1 |           3 |    43.14 ms |    36.734 ms |   2.013 ms |
    /// </summary>
    [ShortRunJob]
    public class ArgumentsBenchmarks
    {
        // [Benchmark]
        // public void ObjectWithJsonSerialization()
        // {
        //     var variables = new QueryVariables {
        //         { "names", new { Name = "Lisa", LastName = "Simpson" } }
        //     };

        //     for (int i = 0; i < 10000; i++)
        //     {
        //         var val = ExpressionUtil.ChangeType(variables["names"], typeof(InputType), true);
        //     }
        // }
        // [Benchmark]
        // public void ListWithJsonSerialization()
        // {
        //     var variables = new QueryVariables {
        //         { "names", new List<InputType2>{new InputType2{ Name = "Lisa", LastName = "Simpson" } }}
        //     };

        //     for (int i = 0; i < 10000; i++)
        //     {
        //         var val = ExpressionUtil.ChangeType(variables["names"], typeof(List<InputType>), true);
        //     }
        // }

        [Benchmark]
        public static void ObjectWithReflection()
        {
            var variables = new QueryVariables {
                { "names", new { Name = "Lisa", LastName = "Simpson" } }
            };

            for (int i = 0; i < 10000; i++)
            {
                ExpressionUtil.ChangeType(variables["names"], typeof(InputType), null);
            }
        }

        [Benchmark]
        public static void ListWithReflection()
        {
            var variables = new QueryVariables {
                { "names", new List<InputType2>{new InputType2{ Name = "Lisa", LastName = "Simpson" } }}
            };

            for (int i = 0; i < 10000; i++)
            {
                ExpressionUtil.ChangeType(variables["names"], typeof(List<InputType>), null);
            }
        }
    }

    // this would be the anonymous class created in compiling the query
    internal class InputType
    {
        public string name = string.Empty;
        public string lastName = string.Empty;
    }

    // This would be the class they pass in that matches the schema but is different from the anonymous class
    internal class InputType2
    {
        public string Name = string.Empty;
        public string LastName = string.Empty;
    }
}