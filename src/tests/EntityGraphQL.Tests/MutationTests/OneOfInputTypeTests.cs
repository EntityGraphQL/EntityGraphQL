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

            var gql = new QueryRequest
            {
                Query = @"
                    query IntrospectionQuery {
                      __type(name: ""InputObject"") {
                        name
                        kind
                        oneField
                      }
                    }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };

            var res = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);

            Assert.Equal("InputObject", ((dynamic)res.Data["__type"]).name);
            Assert.Equal("INPUT_OBJECT", ((dynamic)res.Data["__type"]).kind);
            Assert.False(((dynamic)res.Data["__type"]).oneField);
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

            var gql = new QueryRequest
            {
                Query = @"
                    query IntrospectionQuery {
                      __type(name: ""InputObject"") {
                        name
                        kind
                        oneField
                      }
                    }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };

            var res = schemaProvider.ExecuteRequest(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);

            Assert.Equal("InputObject", ((dynamic)res.Data["__type"]).name);
            Assert.Equal("INPUT_OBJECT", ((dynamic)res.Data["__type"]).kind);
            Assert.True(((dynamic)res.Data["__type"]).oneField);
        }
    }
}
