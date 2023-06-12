using Xunit;
using EntityGraphQL.Tests.ApiVersion1;
using System.Linq;
using System;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Tests
{
    public class SchemaProviderTests
    {
        [Fact]
        public void ReadsContextType()
        {
            var provider = new TestObjectGraphSchema();
            Assert.Equal(typeof(TestDataContext), provider.QueryContextType);
        }
        [Fact]
        public void ExposesFieldsFromObjectWhenNotDefined()
        {
            var provider = new TestObjectGraphSchema();
            Assert.True(provider.Type("Location").HasField("id", null));
            Assert.True(provider.Type("Location").HasField("address", null));
            Assert.True(provider.Type("Location").HasField("state", null));
            Assert.True(provider.Type("Location").HasField("country", null));
            Assert.True(provider.Type("Location").HasField("planet", null));
        }
        [Fact]
        public void ExposesDefinedFields()
        {
            var provider = new TestObjectGraphSchema();
            Assert.True(provider.Type("Person").HasField("id", null));
            Assert.True(provider.Type("Person").HasField("name", null));
            // Not exposed in our schema
            Assert.True(provider.Type("Person").HasField("fullName", null));
        }
        [Fact]
        public void ReturnsActualName()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("id", schema.Type("Project").GetField("id", null).Name);
            Assert.Equal("name", schema.Type("Project").GetField("name", null).Name);
        }
        [Fact]
        public void SupportsEnum()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("Gender", schema.Type("Gender").Name);
            Assert.True(schema.Type("Gender").IsEnum);
            Assert.Equal(4, schema.Type("Gender").GetFields().Count());
            Assert.Equal("__typename", schema.Type("Gender").GetFields().ElementAt(0).Name);
            Assert.Equal("Female", schema.Type("Gender").GetFields().ElementAt(1).Name);
        }
        [Fact]
        public void RemovesTypeAndFields()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("id", schema.Type("Project").GetField("id", null).Name);
            schema.RemoveTypeAndAllFields<Project>();
            Assert.Empty(schema.Query().GetFields().Where(s => s.ReturnType.SchemaType.Name == "project"));
        }
        [Fact]
        public void RemovesTypeAndFields2()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("id", schema.Type("Project").GetField("id", null).Name);
            schema.RemoveTypeAndAllFields("Project");
            Assert.Empty(schema.Query().GetFields().Where(s => s.ReturnType.SchemaType.Name == "project"));
        }

        [Fact]
        public void SupportsAbstract()
        {
            var schema = new TestAbstractDataGraphSchema();
            Assert.Equal("Animal", schema.Type("Animal").Name);
            Assert.True(schema.Type("Animal").IsInterface);

            Assert.Equal("Dog", schema.Type("Dog").Name);
            Assert.False(schema.Type("Dog").IsInterface);
            Assert.Equal("Animal", schema.Type("Dog").BaseTypesReadOnly[0].Name);

            Assert.Equal("Cat", schema.Type("Cat").Name);
            Assert.False(schema.Type("Cat").IsInterface);
            Assert.Equal("Animal", schema.Type("Cat").BaseTypesReadOnly[0].Name);
        }
        [Fact]
        public void HasTypeChecksMappings()
        {
            var schema = new TestObjectGraphSchema();
            Assert.True(schema.HasType(typeof(byte[])));
        }

        [Fact]
        public void Scalar_SpecifiedBy()
        {
            var schema = new TestObjectGraphSchema();
            schema.Type<int>().SpecifiedBy("https://www.example.com");
            var sdl = schema.ToGraphQLSchemaString();

            Assert.Contains("scalar Int @specifiedBy(url: \"https://www.example.com\")", sdl);

            var gql = new QueryRequest
            {
                Query = @"
                    query IntrospectionQuery {
                        __type(name: ""Int"") {
                            name
                            specifiedByURL
                        }
                    }
                "
            };

            var context = new TestDataContext();

            var res = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(res.Errors);

            var schemaType = (dynamic)((dynamic)res.Data["__type"]);
            Assert.Equal("https://www.example.com", schemaType.specifiedByURL);
        }

        [Fact]
        public void Scalar_SpecifiedBy_ErrorsOnObjectType()
        {
            var schema = new TestObjectGraphSchema();

            var ex = Assert.Throws<EntityQuerySchemaException>(() => { schema.Type<Person>().SpecifiedBy("https://www.example.com"); });

            Assert.Equal("@specifiedBy can only be used on scalars", ex.Message);
        }
    }
}