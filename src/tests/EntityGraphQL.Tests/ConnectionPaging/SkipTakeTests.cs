using EntityGraphQL.Schema.Connections;
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
            var take = ConnectionPagingExtension.GetTakeNumber(args);
            Assert.Null(take);

            var skip = ConnectionPagingExtension.GetSkipNumber(args);
            Assert.Equal(0, skip);
        }
        [Fact]
        public void TestOnlyFirst()
        {
            var args = new ConnectionArgs
            {
                first = 3
            };
            var take = ConnectionPagingExtension.GetTakeNumber(args);
            Assert.Equal(3, take);

            var skip = ConnectionPagingExtension.GetSkipNumber(args);
            Assert.Equal(0, skip);
        }
        [Fact]
        public void TestFirstAndAfter()
        {
            var args = new ConnectionArgs
            {
                first = 3,
                afterNum = 2
            };
            var take = ConnectionPagingExtension.GetTakeNumber(args);
            Assert.Equal(3, take);

            var skip = ConnectionPagingExtension.GetSkipNumber(args);
            Assert.Equal(2, skip);
        }
        [Fact]
        public void TestOnlyLast()
        {
            var args = new ConnectionArgs
            {
                last = 3,
                totalCount = 10
            };
            var take = ConnectionPagingExtension.GetTakeNumber(args);
            Assert.Equal(3, take);

            var skip = ConnectionPagingExtension.GetSkipNumber(args);
            Assert.Equal(7, skip);
        }
        [Fact]
        public void TestLastAndBefore()
        {
            var args = new ConnectionArgs
            {
                last = 4,
                beforeNum = 7,
            };
            var take = ConnectionPagingExtension.GetTakeNumber(args);
            Assert.Equal(4, take);

            var skip = ConnectionPagingExtension.GetSkipNumber(args);
            Assert.Equal(3, skip);
        }
        [Fact]
        public void TestOnlyBefore()
        {
            var args = new ConnectionArgs
            {
                beforeNum = 7,
            };
            var take = ConnectionPagingExtension.GetTakeNumber(args);
            Assert.Equal(6, take);

            var skip = ConnectionPagingExtension.GetSkipNumber(args);
            Assert.Equal(0, skip);
        }
        [Fact]
        public void TestOnlyAfter()
        {
            var args = new ConnectionArgs
            {
                afterNum = 5,
            };
            var take = ConnectionPagingExtension.GetTakeNumber(args);
            Assert.Null(take);

            var skip = ConnectionPagingExtension.GetSkipNumber(args);
            Assert.Equal(5, skip);
        }
    }
}

