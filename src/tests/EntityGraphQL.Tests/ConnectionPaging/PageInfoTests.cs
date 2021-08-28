using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.ConnectionPaging
{
    public class PageInfoTests
    {
        [Fact]
        public void TestAllNull()
        {
            var args = new ConnectionArgs();
            var info = new ConnectionPageInfo(10, args);

            Assert.False(info.HasNextPage);
            Assert.False(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(1), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(10), info.EndCursor);
        }

        [Fact]
        public void TestOnlyFirst()
        {
            var args = new ConnectionArgs
            {
                First = 4
            };
            var info = new ConnectionPageInfo(10, args);

            Assert.True(info.HasNextPage);
            Assert.False(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(1), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(4), info.EndCursor);
        }
        [Fact]
        public void TestFirstAndAfter()
        {
            var args = new ConnectionArgs
            {
                First = 4,
                AfterNum = 3
            };
            var info = new ConnectionPageInfo(10, args);

            Assert.True(info.HasNextPage);
            Assert.True(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(4), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(7), info.EndCursor);
        }
        [Fact]
        public void TestOnlyLast()
        {
            var args = new ConnectionArgs
            {
                Last = 3
            };
            var info = new ConnectionPageInfo(10, args);

            Assert.False(info.HasNextPage);
            Assert.True(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(8), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(10), info.EndCursor);
        }
        [Fact]
        public void TestLastAndBefore()
        {
            var args = new ConnectionArgs
            {
                Last = 3,
                BeforeNum = 6
            };
            var info = new ConnectionPageInfo(10, args);

            Assert.True(info.HasNextPage);
            Assert.True(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(3), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(5), info.EndCursor);
        }
        [Fact]
        public void TestOnlyBefore()
        {
            var args = new ConnectionArgs
            {
                BeforeNum = 6
            };
            var info = new ConnectionPageInfo(10, args);

            Assert.True(info.HasNextPage);
            Assert.False(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(1), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(5), info.EndCursor);
        }
        [Fact]
        public void TestOnlyAfter()
        {
            var args = new ConnectionArgs
            {
                AfterNum = 6
            };
            var info = new ConnectionPageInfo(10, args);

            Assert.False(info.HasNextPage);
            Assert.True(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(7), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(10), info.EndCursor);
        }
        [Fact]
        public void TestOverTotal()
        {
            var args = new ConnectionArgs
            {
                First = 5,
                AfterNum = 7
            };
            var info = new ConnectionPageInfo(10, args);

            Assert.False(info.HasNextPage);
            Assert.True(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(8), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(10), info.EndCursor);
        }
        [Fact]
        public void TestBeforeStart()
        {
            var args = new ConnectionArgs
            {
                Last = 5,
                BeforeNum = 3
            };
            var info = new ConnectionPageInfo(10, args);

            Assert.True(info.HasNextPage);
            Assert.False(info.HasPreviousPage);
            Assert.Equal(ConnectionHelper.SerializeCursor(1), info.StartCursor);
            Assert.Equal(ConnectionHelper.SerializeCursor(2), info.EndCursor);
        }
    }
}

