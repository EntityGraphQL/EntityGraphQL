using EntityGraphQL.Compiler.Util;
using Xunit;

namespace EntityGraphQL.Tests;

public class ChangeTypeTests
{
    [Fact]
    public void TestChangeTypeFloat()
    {
        var res = ExpressionUtil.ChangeType("1.2", typeof(float), null);
        Assert.Equal(1.2f, res);
    }
    [Fact]
    public void TestChangeTypeDouble()
    {
        var res = ExpressionUtil.ChangeType("1.2", typeof(double), null);
        Assert.Equal(1.2d, res);
    }
    [Fact]
    public void TestChangeTypeDecimal()
    {
        var res = ExpressionUtil.ChangeType("1.2", typeof(decimal), null);
        Assert.Equal(1.2m, res);
    }
    [Fact]
    public void TestChangeTypeFloatNoFraction()
    {
        var res = ExpressionUtil.ChangeType("1", typeof(float), null);
        Assert.Equal(1f, res);
    }
    [Fact]
    public void TestChangeTypeDoubleNoFraction()
    {
        var res = ExpressionUtil.ChangeType("1", typeof(double), null);
        Assert.Equal(1d, res);
    }
    [Fact]
    public void TestChangeTypeDecimalNoFraction()
    {
        var res = ExpressionUtil.ChangeType("1", typeof(decimal), null);
        Assert.Equal(1m, res);
    }
    [Fact]
    public void TestChangeTypeFloatNull()
    {
        var res = ExpressionUtil.ChangeType("1.2", typeof(float?), null);
        Assert.Equal(1.2f, res);
    }
    [Fact]
    public void TestChangeTypeDoubleNull()
    {
        var res = ExpressionUtil.ChangeType("1.2", typeof(double?), null);
        Assert.Equal(1.2d, res);
    }
    [Fact]
    public void TestChangeTypeDecimalNull()
    {
        var res = ExpressionUtil.ChangeType("1.2", typeof(decimal?), null);
        Assert.Equal(1.2m, res);
    }
    [Fact]
    public void TestChangeTypeFloatNullValue()
    {
        var res = ExpressionUtil.ChangeType(null, typeof(float?), null);
        Assert.Equal((float?)null, res);
    }
    [Fact]
    public void TestChangeTypeDoubleNullValue()
    {
        var res = ExpressionUtil.ChangeType(null, typeof(double?), null);
        Assert.Equal((double?)null, res);
    }
    [Fact]
    public void TestChangeTypeDecimalNullValue()
    {
        var res = ExpressionUtil.ChangeType(null, typeof(decimal?), null);
        Assert.Equal((decimal?)null, res);
    }

    [Fact]
    public void TestImplicitOperator()
    {
        var res = ExpressionUtil.ChangeType(3, typeof(ImplicitOperatorConversionTest), null);
        Assert.NotNull(res);
    }

    public struct ImplicitOperatorConversionTest
    {
        public ImplicitOperatorConversionTest(int value)
        {
            Value = value;
        }

        public int Value { get; }

        /// <summary>
        /// Converts the given TValue into an Optional(TValue)
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator ImplicitOperatorConversionTest(int value)
        {
            return new ImplicitOperatorConversionTest(value);
        }
    }
}