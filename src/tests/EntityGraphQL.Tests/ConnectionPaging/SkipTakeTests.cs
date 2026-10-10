using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.ConnectionPaging;

public class SkipTakeTests
{
    [Fact]
    public void TestAllNull()
    {
        var args = new ConnectionArgs();
        var take = ConnectionHelper.GetTakeNumber(args);
        Assert.Null(take);

        var skip = ConnectionHelper.GetSkipNumber(args);
        Assert.Equal(0, skip);
    }

    [Fact]
    public void TestOnlyFirst()
    {
        var args = new ConnectionArgs { First = 3 };
        var take = ConnectionHelper.GetTakeNumber(args);
        Assert.Equal(3, take);

        var skip = ConnectionHelper.GetSkipNumber(args);
        Assert.Equal(0, skip);
    }

    [Fact]
    public void TestFirstAndAfter()
    {
        var args = new ConnectionArgs { First = 3, AfterNum = 2 };
        var take = ConnectionHelper.GetTakeNumber(args);
        Assert.Equal(3, take);

        var skip = ConnectionHelper.GetSkipNumber(args);
        Assert.Equal(2, skip);
    }

    [Fact]
    public void TestOnlyLast()
    {
        var args = new ConnectionArgs { Last = 3, TotalCount = 10 };
        var take = ConnectionHelper.GetTakeNumber(args);
        Assert.Equal(3, take);

        var skip = ConnectionHelper.GetSkipNumber(args);
        Assert.Equal(7, skip);
    }

    [Fact]
    public void TestLastAndBefore()
    {
        var args = new ConnectionArgs { Last = 4, BeforeNum = 7 };
        var take = ConnectionHelper.GetTakeNumber(args);
        Assert.Equal(4, take);

        var skip = ConnectionHelper.GetSkipNumber(args);
        Assert.Equal(2, skip);
    }

    [Fact]
    public void TestLastAndBefore_WhenLastGreaterThanBeforeNum()
    {
        var args = new ConnectionArgs { Last = 4, BeforeNum = 2 };

        var skip = ConnectionHelper.GetSkipNumber(args);
        Assert.Equal(0, skip);

        var offset = ConnectionHelper.GetSkipNumber(args, false);

        var take = ConnectionHelper.GetTakeNumber(args, offset);
        Assert.Equal(1, take);
    }

    // a root collection's count is known - a before cursor past its end counts back from the end
    [Fact]
    public void TestLastAndBeforePastTheEnd()
    {
        var args = new ConnectionArgs
        {
            Last = 3,
            BeforeNum = 6,
            TotalCount = 3,
        };

        Assert.Equal(0, ConnectionHelper.GetSkipNumber(args, true, true));
        Assert.Equal(3, ConnectionHelper.GetTakeNumber(args, ConnectionHelper.GetSkipNumber(args, false, true)));
        Assert.Equal(1, ConnectionHelper.GetCursorIndex(args, 0, ConnectionHelper.GetSkipNumber(args, false, true), true));
        // not limited - a nested collection's parents share the arguments, so TotalCount is not theirs
        Assert.Equal(2, ConnectionHelper.GetSkipNumber(args));
    }

    [Fact]
    public void TestOnlyBefore()
    {
        var args = new ConnectionArgs { BeforeNum = 7 };
        var take = ConnectionHelper.GetTakeNumber(args);
        Assert.Equal(6, take);

        var skip = ConnectionHelper.GetSkipNumber(args);
        Assert.Equal(0, skip);
    }

    [Fact]
    public void TestOnlyAfter()
    {
        var args = new ConnectionArgs { AfterNum = 5 };
        var take = ConnectionHelper.GetTakeNumber(args);
        Assert.Null(take);

        var skip = ConnectionHelper.GetSkipNumber(args);
        Assert.Equal(5, skip);
    }
}
