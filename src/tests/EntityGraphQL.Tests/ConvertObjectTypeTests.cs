using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class ConvertObjectTypeTests
{
    [Fact]
    public void TestChangeTypeFloat()
    {
        var res = ExpressionUtil.ConvertObjectType("1.2", typeof(float), null);
        Assert.Equal(1.2f, res);
    }

    [Fact]
    public void TestChangeTypeDouble()
    {
        var res = ExpressionUtil.ConvertObjectType("1.2", typeof(double), null);
        Assert.Equal(1.2d, res);
    }

    [Fact]
    public void TestChangeTypeDecimal()
    {
        var res = ExpressionUtil.ConvertObjectType("1.2", typeof(decimal), null);
        Assert.Equal(1.2m, res);
    }

    [Fact]
    public void TestChangeTypeFloatNoFraction()
    {
        var res = ExpressionUtil.ConvertObjectType("1", typeof(float), null);
        Assert.Equal(1f, res);
    }

    [Fact]
    public void TestChangeTypeDoubleNoFraction()
    {
        var res = ExpressionUtil.ConvertObjectType("1", typeof(double), null);
        Assert.Equal(1d, res);
    }

    [Fact]
    public void TestChangeTypeDecimalNoFraction()
    {
        var res = ExpressionUtil.ConvertObjectType("1", typeof(decimal), null);
        Assert.Equal(1m, res);
    }

    [Fact]
    public void TestChangeTypeFloatNull()
    {
        var res = ExpressionUtil.ConvertObjectType("1.2", typeof(float?), null);
        Assert.Equal(1.2f, res);
    }

    [Fact]
    public void TestChangeTypeDoubleNull()
    {
        var res = ExpressionUtil.ConvertObjectType("1.2", typeof(double?), null);
        Assert.Equal(1.2d, res);
    }

    [Fact]
    public void TestChangeTypeDecimalNull()
    {
        var res = ExpressionUtil.ConvertObjectType("1.2", typeof(decimal?), null);
        Assert.Equal(1.2m, res);
    }

    [Fact]
    public void TestChangeTypeFloatNullValue()
    {
        var res = ExpressionUtil.ConvertObjectType(null, typeof(float?), null);
        Assert.Equal((float?)null, res);
    }

    [Fact]
    public void TestChangeTypeDoubleNullValue()
    {
        var res = ExpressionUtil.ConvertObjectType(null, typeof(double?), null);
        Assert.Equal((double?)null, res);
    }

    [Fact]
    public void TestChangeTypeDecimalNullValue()
    {
        var res = ExpressionUtil.ConvertObjectType(null, typeof(decimal?), null);
        Assert.Equal((decimal?)null, res);
    }

    [Fact]
    public void TestImplicitOperator()
    {
        var res = ExpressionUtil.ConvertObjectType(3, typeof(ImplicitOperatorConversionTest), null);
        Assert.NotNull(res);
    }

    public class TrackedInput : ArgumentsTracker
    {
        public string? Name;
        public int? Age;
    }

    public class OtherTrackedInput : ArgumentsTracker
    {
        public string? Name;
        public int? Age;
    }

    [Fact]
    public void AnObjectSetsTheMembersItHasInOrder()
    {
        var res = (TrackedInput)ExpressionUtil.ConvertObjectType(new { age = 3, name = (string?)null }, typeof(TrackedInput), null)!;
        Assert.Equal(["Age", "Name"], res.GetSetPropertiesInOrder());
    }

    [Fact]
    public void ATrackedObjectPassesOnOnlyWhatWasSet()
    {
        var from = new OtherTrackedInput { Name = "a" };
        from.MarkAsSet("Name");
        var res = (TrackedInput)ExpressionUtil.ConvertObjectType(from, typeof(TrackedInput), null)!;
        Assert.Equal("a", res.Name);
        Assert.Equal(["Name"], res.GetSetPropertiesInOrder());
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
