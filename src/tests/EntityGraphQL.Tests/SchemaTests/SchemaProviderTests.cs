using Xunit;
using EntityGraphQL.Tests.ApiVersion1;
using System.Linq;
using System;

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
    }
}