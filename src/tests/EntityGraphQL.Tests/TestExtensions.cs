using System;
using System.Collections.Generic;
using EntityGraphQL.Extensions;
using Xunit;

namespace EntityGraphQL.Tests
{
    public class TestExtensions
    {
        [Fact]
        public void TestGetNonNullableOrEnumerableType()
        {
            Assert.Equal(typeof(int), typeof(int[]).GetNonNullableOrEnumerableType());
            Assert.Equal(typeof(double), typeof(List<double>).GetNonNullableOrEnumerableType());
            Assert.Equal(typeof(int), typeof(int).GetNonNullableOrEnumerableType());
            Assert.Equal(typeof(DateTime), typeof(IEnumerable<DateTime>).GetNonNullableOrEnumerableType());
            Assert.Equal(typeof(DateTime), typeof(DateTime?).GetNonNullableOrEnumerableType());
            Assert.Equal(typeof(int), typeof(int?).GetNonNullableOrEnumerableType());
            Assert.Equal(typeof(string), typeof(string).GetNonNullableOrEnumerableType());
        }
    }
}