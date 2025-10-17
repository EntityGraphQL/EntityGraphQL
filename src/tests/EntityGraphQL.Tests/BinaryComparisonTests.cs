using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class BinaryComparisonTests
{
    private readonly EqlCompileContext compileContext = new(new CompileContext(new ExecutionOptions(), null, new QueryRequestContext(null, null), null, null));

    private class GuidHolder
    {
        public GuidHolder(Guid id, Guid? idN, string name)
        {
            Id = id; IdN = idN; Name = name;
        }
        public Guid Id { get; set; }
        public Guid? IdN { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void Guid_Equals_StringLiteral_LeftGuid_RightString()
    {
        var schema = SchemaBuilder.FromObject<GuidHolder>();
        var target = Guid.NewGuid();
        var compiled = EntityQueryCompiler.Compile($"id == \"{target}\"", schema, compileContext);
        var data = new List<GuidHolder>
        {
            new(Guid.NewGuid(), null, "A"),
            new(target, null, "B"),
        };
        var res = data.Where((Func<GuidHolder, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Single(res);
        Assert.Equal("B", res[0].Name);
    }

    [Fact]
    public void Guid_Equals_StringLiteral_LeftString_RightGuid()
    {
        var schema = SchemaBuilder.FromObject<GuidHolder>();
        var target = Guid.NewGuid();
        var compiled = EntityQueryCompiler.Compile($"\"{target}\" == id", schema, compileContext);
        var data = new List<GuidHolder>
        {
            new(Guid.NewGuid(), null, "A"),
            new(target, null, "B"),
        };
        var res = data.Where((Func<GuidHolder, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Single(res);
        Assert.Equal("B", res[0].Name);
    }

    [Fact]
    public void NullableGuid_Equals_StringLiteral()
    {
        var schema = SchemaBuilder.FromObject<GuidHolder>();
        var target = Guid.NewGuid();
        var compiled = EntityQueryCompiler.Compile($"idN == \"{target}\"", schema, compileContext);
        var data = new List<GuidHolder>
        {
            new(Guid.NewGuid(), null, "A"),
            new(Guid.NewGuid(), target, "B"),
        };
        var res = data.Where((Func<GuidHolder, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Single(res);
        Assert.Equal("B", res[0].Name);
    }

    private enum EColor { Red, Green, Blue }

    private class EnumHolder
    {
        public EnumHolder(EColor color, string name) { Color = color; Name = name; }
        public EColor Color { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void Enum_Equals_StringLiteral()
    {
        var schema = SchemaBuilder.FromObject<EnumHolder>();
        var compiled = EntityQueryCompiler.Compile("color == \"Green\"", schema, compileContext);
        var data = new List<EnumHolder>
        {
            new(EColor.Red, "A"),
            new(EColor.Green, "B"),
        };
        var res = data.Where((Func<EnumHolder, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Single(res);
        Assert.Equal("B", res[0].Name);
    }

    private class NumericHolder
    {
        public NumericHolder(int priceI, uint priceU, decimal priceD, double priceF, int? n, string name)
        { PriceI = priceI; PriceU = priceU; PriceD = priceD; PriceF = priceF; N = n; Name = name; }
        public int PriceI { get; set; }
        public uint PriceU { get; set; }
        public decimal PriceD { get; set; }
        public double PriceF { get; set; }
        public int? N { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void Numeric_Int_Vs_Decimal_Promotion()
    {
        var schema = SchemaBuilder.FromObject<NumericHolder>();
        var compiled = EntityQueryCompiler.Compile("priceI > 3.5", schema, compileContext);
        var data = new List<NumericHolder>
        {
            new(3, 3, 3m, 3.0, 1, "A"),
            new(4, 4, 4m, 4.0, 2, "B"),
        };
        var res = data.Where((Func<NumericHolder, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Single(res);
        Assert.Equal("B", res[0].Name);
    }

    [Fact]
    public void Numeric_Int_Vs_UInt_Alignment()
    {
        var schema = SchemaBuilder.FromObject<NumericHolder>();
        var compiled = EntityQueryCompiler.Compile("priceU == 4", schema, compileContext);
        var data = new List<NumericHolder>
        {
            new(1, 3u, 0m, 0.0, null, "A"),
            new(2, 4u, 0m, 0.0, null, "B"),
        };
        var res = data.Where((Func<NumericHolder, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Single(res);
        Assert.Equal("B", res[0].Name);
    }

    [Fact]
    public void Nullable_Int_Equals_Constant()
    {
        var schema = SchemaBuilder.FromObject<NumericHolder>();
        var compiled = EntityQueryCompiler.Compile("n == 2", schema, compileContext);
        var data = new List<NumericHolder>
        {
            new(0, 0, 0m, 0.0, 1, "A"),
            new(0, 0, 0m, 0.0, 2, "B"),
        };
        var res = data.Where((Func<NumericHolder, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Single(res);
        Assert.Equal("B", res[0].Name);
    }
}
