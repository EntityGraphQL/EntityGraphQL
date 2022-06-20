using EntityGraphQL.Compiler.Util;
using Xunit;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using System.Collections.Generic;

namespace EntityGraphQL.Tests.Util
{
    public class NullableReferenceTypeTests
    {
        public class Test { }

        public class WithoutNullableRefEnabled
        {
            public int NonNullableInt { get; set; }
            public int? NullableInt { get; set; }
            public string Nullable { get; set; }
            public IEnumerable<Test> Tests { get; set; }
            public IEnumerable<Test> NullableMethod() { return null!; }
        }

        [Fact]
        public void TestNullableWithoutNullableRefEnabled()
        {
            var propertyInfo = typeof(WithoutNullableRefEnabled).GetProperty("NonNullableInt");            
            Assert.False(propertyInfo.IsNullable());

            propertyInfo = typeof(WithoutNullableRefEnabled).GetProperty("NullableInt");
            Assert.True(propertyInfo.IsNullable());

            propertyInfo = typeof(WithoutNullableRefEnabled).GetProperty("Nullable");
            Assert.True(propertyInfo.IsNullable());

            propertyInfo = typeof(WithoutNullableRefEnabled).GetProperty("Tests");
            Assert.True(propertyInfo.IsNullable());

            var methodInfo = typeof(WithoutNullableRefEnabled).GetMethod("NullableMethod");
            Assert.True(methodInfo.IsNullable());

            var schema = SchemaBuilder.FromObject<WithoutNullableRefEnabled>();
            var schemaString = schema.ToGraphQLSchemaString();

            Assert.Contains(@"nonNullableInt: Int!", schemaString);
            Assert.Contains(@"nullableInt: Int", schemaString);
            Assert.Contains(@"nullable: String", schemaString);
            Assert.Contains(@"tests: [Test!]", schemaString);
        }

#nullable enable
        public class WithNullableRefEnabled
        {
            public int NonNullableInt { get; set; }
            public int? NullableInt { get; set; }
            public string NonNullable { get; set; } = "";
            public string? Nullable { get; set; }
            public IEnumerable<Test> Tests { get; set; } = new List<Test>();
            public IEnumerable<Test>? Tests2 { get; set; }
            public IEnumerable<Test> NonNullableMethod() { return null!; }
            public IEnumerable<Test?> NullableMethod() { return null!; }
        }
#nullable restore

        [Fact]
        public void TestNullableWithNullableRefEnabled()
        {
            var propertyInfo = typeof(WithoutNullableRefEnabled).GetProperty("NonNullableInt");
            Assert.False(propertyInfo.IsNullable());

            propertyInfo = typeof(WithoutNullableRefEnabled).GetProperty("NullableInt");
            Assert.True(propertyInfo.IsNullable());

            propertyInfo = typeof(WithNullableRefEnabled).GetProperty("Nullable");
            Assert.True(propertyInfo.IsNullable());

            propertyInfo = typeof(WithNullableRefEnabled).GetProperty("NonNullable");
            Assert.False(propertyInfo.IsNullable());

            var methodInfo = typeof(WithNullableRefEnabled).GetMethod("NullableMethod");
            Assert.True(methodInfo.IsNullable());

            methodInfo = typeof(WithNullableRefEnabled).GetMethod("NonNullableMethod");
            Assert.False(methodInfo.IsNullable());

            var schema = SchemaBuilder.FromObject<WithNullableRefEnabled>();
            var schemaString = schema.ToGraphQLSchemaString();

            Assert.Contains(@"nonNullableInt: Int!", schemaString);
            Assert.Contains(@"nullableInt: Int", schemaString);
            Assert.Contains(@"nullable: String", schemaString);
            Assert.Contains(@"nonNullable: String!", schemaString);
            Assert.Contains(@"tests: [Test!]!", schemaString);
            Assert.Contains(@"tests2: [Test!]", schemaString);
        }

    }
}