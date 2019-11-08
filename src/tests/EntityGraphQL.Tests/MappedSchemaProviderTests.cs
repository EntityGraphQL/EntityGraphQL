using Xunit;
using EntityGraphQL.Tests.ApiVersion1;
using System.Linq;

namespace EntityGraphQL.Tests
{
    public class MappedSchemaProviderTests
    {
        [Fact]
        public void ReadsContextType()
        {
            var provider = new TestObjectGraphSchema();
            Assert.Equal(typeof(TestDataContext), provider.ContextType);
        }
        [Fact]
        public void ExposesFieldsFromObjectWhenNotDefined()
        {
            var provider = new TestObjectGraphSchema();
            Assert.True(provider.TypeHasField("Location", "id", new string[0]));
            Assert.True(provider.TypeHasField("Location", "address", new string[0]));
            Assert.True(provider.TypeHasField("Location", "state", new string[0]));
            Assert.True(provider.TypeHasField("Location", "country", new string[0]));
            Assert.True(provider.TypeHasField("Location", "planet", new string[0]));
        }
        [Fact]
        public void ExposesDefinedFields()
        {
            var provider = new TestObjectGraphSchema();
            Assert.True(provider.TypeHasField("OpenTask", "id", new string[0]));
            Assert.True(provider.TypeHasField("OpenTask", "assignee", new string[0]));
            // Not exposed in our schema
            Assert.False(provider.TypeHasField("OpenTask", "isActive", new string[0]));
        }
        [Fact]
        public void ReturnsActualName()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("id", schema.GetActualFieldName("Project", "id"));
            Assert.Equal("name", schema.GetActualFieldName("Project", "name"));
        }
        [Fact]
        public void RemovesTypeAndFields()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("id", schema.GetActualFieldName("Project", "id"));
            schema.RemoveTypeAndAllFields<Project>();
            Assert.Empty(schema.GetQueryFields().Where(s => s.ReturnTypeClrSingle == "project"));
        }
        [Fact]
        public void RemovesTypeAndFields2()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("id", schema.GetActualFieldName("Project", "id"));
            schema.RemoveTypeAndAllFields("Project");
            Assert.Empty(schema.GetQueryFields().Where(s => s.ReturnTypeClrSingle == "project"));
        }
    }
}