using EntityGraphQL.Schema.Connections;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.ConnectionPaging
{
    public class GetCursorTests
    {
        [Fact]
        public void TestAllNull()
        {
            var args = new ConnectionArgs();
            var index = 4;
            var cursor = ConnectionHelper.GetCursor(args, index);
            // non zero based index
            Assert.Equal(ConnectionHelper.SerializeCursor(index + 1), cursor);
        }
        [Fact]
        public void TestOnlyFirst()
        {
            var args = new ConnectionArgs
            {
                first = 4,
            };
            var index = 4;
            var cursor = ConnectionHelper.GetCursor(args, index);
            // non zero based index
            Assert.Equal(ConnectionHelper.SerializeCursor(index + 1), cursor);
        }
        [Fact]
        public void TestFirstAndAfter()
        {
            var args = new ConnectionArgs
            {
                first = 4,
                afterNum = 3
            };
            var index = 3; // index of item is 0 based so this is the 4th item
            var cursor = ConnectionHelper.GetCursor(args, index);
            Assert.Equal(ConnectionHelper.SerializeCursor(7), cursor);
        }
        [Fact]
        public void TestOnlyLast()
        {
            var args = new ConnectionArgs
            {
                last = 4,
                totalCount = 10
            };
            var index = 3; // 4th item
            var cursor = ConnectionHelper.GetCursor(args, index);
            Assert.Equal(ConnectionHelper.SerializeCursor(10), cursor);
        }
        [Fact]
        public void TestLastAndBefore()
        {
            var args = new ConnectionArgs
            {
                last = 4,
                beforeNum = 6,
                totalCount = 10
            };
            var index = 1; // 2nd item
            var cursor = ConnectionHelper.GetCursor(args, index);
            Assert.Equal(ConnectionHelper.SerializeCursor(3), cursor);
        }
        [Fact]
        public void TestLastAndBefore2()
        {
            var args = new ConnectionArgs
            {
                last = 3,
                beforeNum = 4,
                totalCount = 10
            };
            var index = 0; // 1st item
            var cursor = ConnectionHelper.GetCursor(args, index);
            Assert.Equal(ConnectionHelper.SerializeCursor(1), cursor);
        }
        [Fact]
        public void TestOnlyBefore()
        {
            var args = new ConnectionArgs
            {
                beforeNum = 4,
                totalCount = 10
            };
            var index = 0; // 1st item
            var cursor = ConnectionHelper.GetCursor(args, index);
            Assert.Equal(ConnectionHelper.SerializeCursor(1), cursor);
        }
        [Fact]
        public void TestOnlyBefore2()
        {
            var args = new ConnectionArgs
            {
                beforeNum = 8,
                totalCount = 10
            };
            var index = 3; // 4th item
            var cursor = ConnectionHelper.GetCursor(args, index);
            Assert.Equal(ConnectionHelper.SerializeCursor(4), cursor);
        }
        [Fact]
        public void TestOnlyAfter()
        {
            var args = new ConnectionArgs
            {
                afterNum = 7,
            };
            var index = 1; // 2nd item
            var cursor = ConnectionHelper.GetCursor(args, index);
            Assert.Equal(ConnectionHelper.SerializeCursor(9), cursor);
        }
    }
}

