using EntityGraphQL.Schema;
using EntityGraphQL.Schema.Directives;
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
            Assert.Contains("one: Int!", schema);
            Assert.Contains("two: Int!", schema);

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

            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
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
            Assert.Contains("one: Int", schema);
            Assert.Contains("two: Int", schema);

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

            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);

            Assert.Equal("InputObject", ((dynamic)res.Data["__type"]).name);
            Assert.Equal("INPUT_OBJECT", ((dynamic)res.Data["__type"]).kind);
            Assert.True(((dynamic)res.Data["__type"]).oneField);
        }
        [Fact]
        public void TestOneOfAttributeCanNotBeUsedOnNonInputTypes()
        {
            var schemaProvider = SchemaBuilder.Create<TestDataContext>();
            var ex = Assert.Throws<EntityQuerySchemaException>(() => schemaProvider.AddType<OneOfInputType>("InputObject", "Using an object in the arguments"));
            Assert.Equal($"OneOfInputType marked with OneOfDirective directive which is not valid on a {nameof(GqlTypes.QueryObject)}", ex.Message);
        }

        [GraphQLOneOf]
        private class InvalidOneOfInputType
        {
            public int One { get; set; }
            public int? Two { get; set; }
        }

        [Fact]
        public void TestOneOfAttributeChecksFieldsAreNullable()
        {
            var schemaProvider = SchemaBuilder.Create<TestDataContext>();
            var ex = Assert.Throws<EntityQuerySchemaException>(() => schemaProvider.AddInputType<InvalidOneOfInputType>("InputObject", "Using an object in the arguments").AddAllFields());
            Assert.Equal("InvalidOneOfInputType is a OneOf type but all its fields are not nullable. OneOf input types require all the field to be nullable.", ex.Message);
        }

        [Fact]
        public void TestOneOfErrorsIfMoreThanOneOfSupplied()
        {
            var schemaProvider = SchemaBuilder.Create<TestDataContext>();
            schemaProvider.AddInputType<OneOfInputType>("InputObject", "Using an object in the arguments").AddAllFields();
            schemaProvider.Mutation().Add("createOneOfInputType", (OneOfInputType input) => true);

            var gql = new QueryRequest
            {
                Query = @"
                    mutation Test {
                        createOneOfInputType(input: { one: 1, two: 2 })
                    }"
            };

            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.NotNull(res.Errors);
            Assert.Equal("Exactly one field must be specified for argument of type InputObject.", res.Errors[0].Message);
        }


        [Fact]
        public void TestOneOfWorksWithInlineParameters()
        {
            var schemaProvider = SchemaBuilder.Create<TestDataContext>();
            schemaProvider.AddInputType<OneOfInputType>("InputObject", "Using an object in the arguments").AddAllFields();
            schemaProvider.Mutation().Add("createOneOfInputType", (OneOfInputType input) => input.One == 1 && input.Two == null);

            var gql = new QueryRequest
            {
                Query = @"
                    mutation Test {
                        createOneOfInputType(input: { one: 1 })
                    }"
            };

            var res = schemaProvider.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Null(res.Errors);
            Assert.True((bool)res.Data["createOneOfInputType"]);

        }
    }
}
