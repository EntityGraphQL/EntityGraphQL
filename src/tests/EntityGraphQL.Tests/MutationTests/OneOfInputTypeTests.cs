using EntityGraphQL.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace EntityGraphQL.Tests
{
    public class OneOfInputTypeTests
    {
        private class NotOneOfInputType
        {
            public int One { get; set; }
            public int Two { get; set; }
        }

        [Fact]
        public void TestNotOneOfAttribute()
        {
            var schemaProvider = SchemaBuilder.Create<TestDataContext>();
            schemaProvider.AddInputType<NotOneOfInputType>("InputObject", "Using an object in the arguments").AddAllFields();

            var schema = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("input InputObject {", schema);
            Assert.Contains("one: Int!\r\n", schema);
            Assert.Contains("two: Int!\r\n", schema);
        }

        [GraphQLOneOf]
        private class OneOfInputType
        {
            public int? One { get; set; }
            public int? Two { get; set; }
        }

        [Fact]
        public void TestOneOfAttribute()
        {
            var schemaProvider = SchemaBuilder.Create<TestDataContext>();
            schemaProvider.AddInputType<OneOfInputType>("InputObject", "Using an object in the arguments").AddAllFields();

            var schema = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("input InputObject @oneOf {", schema);
            Assert.Contains("one: Int\r\n", schema);
            Assert.Contains("two: Int\r\n", schema);
        }
    }
}
