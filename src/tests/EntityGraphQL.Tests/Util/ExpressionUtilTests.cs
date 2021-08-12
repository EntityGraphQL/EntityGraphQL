using EntityGraphQL.Compiler.Util;
using Xunit;

namespace EntityGraphQL.Tests.Util
{
    public class ExpressionUtilTests
    {
        [Fact]
        public void TestMergeTypesObj1Null()
        {
            var obj2 = new
            {
                hi = "world"
            };

            var result = ExpressionUtil.MergeTypes(null, obj2.GetType());

            Assert.NotNull(result);
            Assert.Single(result.GetProperties());
        }

        [Fact]
        public void TestMergeTypes()
        {
            object obj1 = new
            {
                world = "hi"
            };

            var obj2 = new
            {
                hi = "world"
            };

            var result = ExpressionUtil.MergeTypes(obj1.GetType(), obj2.GetType());

            Assert.NotNull(result);
            Assert.Equal(2, result.GetFields().Length);
        }
    }
}