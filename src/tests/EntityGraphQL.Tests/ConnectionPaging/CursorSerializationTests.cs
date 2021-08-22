using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.ConnectionPaging
{
    public class CursorSerializationTests
    {
        [Fact]
        public void TestSerializeAndDeserialize()
        {
            const int val = 0;
            var cursor = ConnectionPagingExtension.SerializeCursor(val, null);
            var valBack = ConnectionPagingExtension.DeserializeCursor(cursor);

            Assert.NotNull(valBack);
            Assert.Equal(val, valBack);
        }

        [Fact]
        public void TestSerializeAndDeserializeWithOffset()
        {
            const int val = 2;
            const int offset = 3;
            var cursor = ConnectionPagingExtension.SerializeCursor(val, offset);
            var valBack = ConnectionPagingExtension.DeserializeCursor(cursor);

            Assert.NotNull(valBack);
            Assert.Equal(val + offset, valBack);
        }

        [Fact]
        public void TestSerializeAndDeserializeLarge()
        {
            const int val = int.MaxValue;
            var cursor = ConnectionPagingExtension.SerializeCursor(val, null);
            var valBack = ConnectionPagingExtension.DeserializeCursor(cursor);

            Assert.NotNull(valBack);
            Assert.Equal(val, valBack);
        }
    }
}