using System;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;

namespace Benchmarks;

/// <summary>
/// Benchmarking the performance of parsing and compiling the filter language used in the EntityGraphQL filter arg and else where
/// On a Apple M1 Max 64GB ram
/// Command: `dotnet run -c Release`
///
/// 5.3.0
/// |                          Method |      Toolchain | IterationCount | LaunchCount | WarmupCount |     Mean |     Error |   StdDev |
/// |-------------------------------- |--------------- |--------------- |------------ |------------ |---------:|----------:|---------:|
/// |                SimpleExpression |        Default |              3 |           1 |           3 | 14.16 us |  0.948 us | 0.052 us |
/// |               ComplexExpression |        Default |              3 |           1 |           3 | 51.51 us | 85.437 us | 4.683 us |
/// | ComplexWithMethodCallExpression |        Default |              3 |           1 |           3 | 66.06 us |  5.265 us | 0.289 us |
///
/// 5.4.0 with Parlot replacing Antlr
/// |                SimpleExpression |        Default |              3 |           1 |           3 |  6.380 us | 0.2524 us | 0.0138 us |
/// |               ComplexExpression |        Default |              3 |           1 |           3 | 24.810 us | 2.7608 us | 0.1513 us |
/// | ComplexWithMethodCallExpression |        Default |              3 |           1 |           3 | 45.103 us | 5.0855 us | 0.2788 us |
///
/// 5.6.0 with net9.0 Parlot 1.1.0
///
/// | Method                          | Toolchain              | IterationCount | LaunchCount | WarmupCount | Mean     | Error     | StdDev   |
/// |-------------------------------- |----------------------- |--------------- |------------ |------------ |---------:|----------:|---------:|
/// | SimpleExpression                | InProcessEmitToolchain | Default        | Default     | Default     | 11.09 us |  0.075 us | 0.070 us |
/// | ComplexExpression               | InProcessEmitToolchain | Default        | Default     | Default     | 32.07 us |  0.233 us | 0.218 us |
/// | ComplexWithMethodCallExpression | InProcessEmitToolchain | Default        | Default     | Default     | 54.36 us |  0.181 us | 0.160 us |
/// | SimpleExpression                | Default                | 3              | 1           | 3           | 10.62 us |  3.848 us | 0.211 us |
/// | ComplexExpression               | Default                | 3              | 1           | 3           | 32.84 us | 23.009 us | 1.261 us |
/// | ComplexWithMethodCallExpression | Default                | 3              | 1           | 3           | 53.69 us |  9.651 us | 0.529 us |
/// </summary>
[ShortRunJob]
public class EqlBenchmarks : BaseBenchmark
{
    [Benchmark]
    public void SimpleExpression()
    {
        var expressionStr = "name == \"foo\"";
        var data = new Movie(Guid.NewGuid(), "foo", 3, new DateTime(2021, 1, 1), new Person(Guid.NewGuid(), "Jimmy", "Rum", new DateTime(1978, 2, 4), []), [], new MovieGenre("Action"));
        var context = Expression.Parameter(data.GetType(), "m");
        var expression = EntityQueryCompiler.CompileWith(expressionStr, context, Schema, new QueryRequestContext(null, null), new ExecutionOptions());
    }

    [Benchmark]
    public void ComplexExpression()
    {
        var expressionStr = "name == \"foo\" && director.name == \"Jimmy\" && director.dob > \"1978-02-04\" && genre.name == \"Action\" && rating > 3";
        var data = new Movie(Guid.NewGuid(), "foo", 3, new DateTime(2021, 1, 1), new Person(Guid.NewGuid(), "Jimmy", "Rum", new DateTime(1978, 2, 4), []), [], new MovieGenre("Action"));
        var context = Expression.Parameter(data.GetType(), "m");
        var expression = EntityQueryCompiler.CompileWith(expressionStr, context, Schema, new QueryRequestContext(null, null), new ExecutionOptions());
    }

    [Benchmark]
    public void ComplexWithMethodCallExpression()
    {
        var expressionStr =
            "name.contains(\"fo\") && director.name.toLower().startsWith(\"ji\") && director.dob > \"1978-01-01\" && actors.orderBy(name).first().name.startsWith(\"bob\") && rating > 3";
        var data = new Movie(Guid.NewGuid(), "foo", 3, new DateTime(2021, 1, 1), new Person(Guid.NewGuid(), "Jimmy", "Rum", new DateTime(1978, 2, 4), []), [], new MovieGenre("Action"));
        var context = Expression.Parameter(data.GetType(), "m");
        var expression = EntityQueryCompiler.CompileWith(expressionStr, context, Schema, new QueryRequestContext(null, null), new ExecutionOptions());
    }
}
