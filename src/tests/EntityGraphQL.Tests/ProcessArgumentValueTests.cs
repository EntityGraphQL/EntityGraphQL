using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using HotChocolate.Language;
using Xunit;

namespace EntityGraphQL.Tests;

public class ProcessArgumentValueTests
{
    [Fact]
    public void TestParseArgumentFloat()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new FloatValueNode(1.2), "arg", typeof(float));
        Assert.Equal(1.2f, res);
    }
    [Fact]
    public void TestParseArgumentDouble()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new FloatValueNode(1.2), "arg", typeof(double));
        Assert.Equal(1.2d, res);
    }
    [Fact]
    public void TestParseArgumentDecimal()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new FloatValueNode(1.2), "arg", typeof(decimal));
        Assert.Equal(1.2m, res);
    }
    [Fact]
    public void TestParseArgumentFloatNoFraction()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new IntValueNode(1), "arg", typeof(float));
        Assert.Equal(1f, res);
    }
    [Fact]
    public void TestParseArgumentDoubleNoFraction()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new IntValueNode(1), "arg", typeof(double));
        Assert.Equal(1d, res);
    }
    [Fact]
    public void TestParseArgumentDecimalNoFraction()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new IntValueNode(1), "arg", typeof(decimal));
        Assert.Equal(1m, res);
    }
    [Fact]
    public void TestParseArgumentFloatNull()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new FloatValueNode(1.2), "arg", typeof(float?));
        Assert.Equal(1.2f, res);
    }
    [Fact]
    public void TestParseArgumentDoubleNull()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new FloatValueNode(1.2), "arg", typeof(double?));
        Assert.Equal(1.2d, res);
    }
    [Fact]
    public void TestParseArgumentDecimalNull()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new FloatValueNode(1.2), "arg", typeof(decimal?));
        Assert.Equal(1.2m, res);
    }

    [Fact]
    public void TestParseArgumentFloatNullValue()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new NullValueNode(null), "arg", typeof(float?));
        Assert.Equal((float?)null, res);
    }
    [Fact]
    public void TestParseArgumentDoubleNullValue()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new NullValueNode(null), "arg", typeof(double?));
        Assert.Equal((double?)null, res);
    }
    [Fact]
    public void TestParseArgumentDecimalNullValue()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = QueryWalkerHelper.ProcessArgumentValue(schema, new NullValueNode(null), "arg", typeof(decimal?));
        Assert.Equal((decimal?)null, res);
    }
}