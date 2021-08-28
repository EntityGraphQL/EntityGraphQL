using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.ConnectionPaging
{
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
            var args = new ConnectionArgs
            {
                First = 3
            };
            var take = ConnectionHelper.GetTakeNumber(args);
            Assert.Equal(3, take);

            var skip = ConnectionHelper.GetSkipNumber(args);
            Assert.Equal(0, skip);
        }
        [Fact]
        public void TestFirstAndAfter()
        {
            var args = new ConnectionArgs
            {
                First = 3,
                AfterNum = 2
            };
            var take = ConnectionHelper.GetTakeNumber(args);
            Assert.Equal(3, take);

            var skip = ConnectionHelper.GetSkipNumber(args);
            Assert.Equal(2, skip);
        }
        [Fact]
        public void TestOnlyLast()
        {
            var args = new ConnectionArgs
            {
                Last = 3,
                TotalCount = 10
            };
            var take = ConnectionHelper.GetTakeNumber(args);
            Assert.Equal(3, take);

            var skip = ConnectionHelper.GetSkipNumber(args);
            Assert.Equal(7, skip);
        }
        [Fact]
        public void TestLastAndBefore()
        {
            var args = new ConnectionArgs
            {
                Last = 4,
                BeforeNum = 7,
            };
            var take = ConnectionHelper.GetTakeNumber(args);
            Assert.Equal(4, take);

            var skip = ConnectionHelper.GetSkipNumber(args);
            Assert.Equal(3, skip);
        }
        [Fact]
        public void TestOnlyBefore()
        {
            var args = new ConnectionArgs
            {
                BeforeNum = 7,
            };
            var take = ConnectionHelper.GetTakeNumber(args);
            Assert.Equal(6, take);

            var skip = ConnectionHelper.GetSkipNumber(args);
            Assert.Equal(0, skip);
        }
        [Fact]
        public void TestOnlyAfter()
        {
            var args = new ConnectionArgs
            {
                AfterNum = 5,
            };
            var take = ConnectionHelper.GetTakeNumber(args);
            Assert.Null(take);

            var skip = ConnectionHelper.GetSkipNumber(args);
            Assert.Equal(5, skip);
        }
    }
}

