using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Compiler;
using Microsoft.Extensions.DependencyInjection;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class DateAndTimeScalarsTests
{
    private readonly EqlCompileContext compileContext = new(new CompileContext(new ExecutionOptions(), null, new QueryRequestContext(null, null), null, null));

#if NET6_0_OR_GREATER
    private class WithDateOnly
    {
        public WithDateOnly(DateOnly d, string name)
        {
            D = d;
            Name = name;
        }
        public DateOnly D { get; set; }
        public string Name { get; set; }
    }

    [Theory]
    [InlineData("\"2020-08-11\"")]
    public void EntityQuery_WorksWithDateOnly(string dateValue)
    {
        var schemaProvider = SchemaBuilder.FromObject<WithDateOnly>();
        var compiled = EntityQueryCompiler.Compile($"d >= {dateValue}", schemaProvider, compileContext);
        var list = new List<WithDateOnly>
        {
            new(new DateOnly(2020, 08, 10), "First"),
            new(new DateOnly(2020, 08, 11), "Second"),
            new(new DateOnly(2020, 08, 12), "Third"),
        };
        var res = list.Where((Func<WithDateOnly, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Equal(2, res.Count);
        Assert.Equal("Second", res[0].Name);
        Assert.Equal("Third", res[1].Name);
    }

    private class WithTimeOnly
    {
        public WithTimeOnly(TimeOnly t, string name)
        {
            T = t;
            Name = name;
        }
        public TimeOnly T { get; set; }
        public string Name { get; set; }
    }

    [Theory]
    [InlineData("\"13:22:11\"", 2)]
    [InlineData("\"13:22:11.3000003\"", 1)]
    public void EntityQuery_WorksWithTimeOnly(string timeValue, int expectedCount)
    {
        var schemaProvider = SchemaBuilder.FromObject<WithTimeOnly>();
        var compiled = EntityQueryCompiler.Compile($"t >= {timeValue}", schemaProvider, compileContext);
        var list = new List<WithTimeOnly>
        {
            new(new TimeOnly(13, 21, 11), "First"),
            new(new TimeOnly(13, 22, 11), "Second"),
            new(new TimeOnly(13, 23, 11), "Third"),
        };
        var res = list.Where((Func<WithTimeOnly, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Equal(expectedCount, res.Count);
        Assert.Equal(res.Last().Name, expectedCount == 2 ? "Third" : "Third");
    }
#endif

    private class WithTimeSpan
    {
        public WithTimeSpan(TimeSpan span, string name)
        {
            Span = span;
            Name = name;
        }
        public TimeSpan Span { get; set; }
        public string Name { get; set; }
    }

    [Theory]
    [InlineData("\"01:02:03\"", 2)]
    [InlineData("\"00:00:00\"", 3)]
    public void EntityQuery_WorksWithTimeSpan(string spanValue, int expectedCount)
    {
        var schemaProvider = SchemaBuilder.FromObject<WithTimeSpan>();
        var compiled = EntityQueryCompiler.Compile($"span >= {spanValue}", schemaProvider, compileContext);
        var list = new List<WithTimeSpan>
        {
            new(TimeSpan.FromHours(1), "First"),
            new(new TimeSpan(1, 2, 3), "Second"),
            new(new TimeSpan(3, 0, 0), "Third"),
        };
        var res = list.Where((Func<WithTimeSpan, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Equal(expectedCount, res.Count);
    }

#if NET6_0_OR_GREATER
    private class MutationContext { }

    private class EchoTypes
    {
        [GraphQLMutation]
        public EchoResult Echo(DateOnly d, TimeOnly t, TimeSpan span)
        {
            return new EchoResult { D = d, T = t, Span = span };
        }
    }

    private class EchoResult
    {
        public DateOnly D { get; set; }
        public TimeOnly T { get; set; }
        public TimeSpan Span { get; set; }
    }

    [Fact]
    public void Mutation_Deserializes_DateOnly_TimeOnly_TimeSpan()
    {
        var schema = new SchemaProvider<MutationContext>();
        schema.AddType<EchoResult>(nameof(EchoResult), null).AddAllFields();
        schema.AddMutationsFrom<EchoTypes>(new SchemaBuilderOptions { AutoCreateInputTypes = true });

        var req = new QueryRequest
        {
            Query = @"mutation m { echo(d: ""2020-08-11"", t: ""13:22:11"", span: ""01:02:03"") { d t span } }",
        };

        var result = schema.ExecuteRequestWithContext(req, new MutationContext(), null, null);
        Assert.Null(result.Errors);
    }
#endif
}
