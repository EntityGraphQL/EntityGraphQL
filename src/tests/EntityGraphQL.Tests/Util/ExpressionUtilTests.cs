using System;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
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
        [Fact]
        public void TestObjectToDictionaryArgs()
        {
            object obj1 = new
            {
                world = "hi"
            };

            var obj2 = new
            {
                hi = "world"
            };

            var argType = ExpressionUtil.MergeTypes(obj1.GetType(), obj2.GetType());
            var allArguments = ExpressionUtil.ObjectToDictionaryArgs(new SchemaProvider<object>(), argType);
            Assert.Equal(2, allArguments.Count);
        }
    }
}