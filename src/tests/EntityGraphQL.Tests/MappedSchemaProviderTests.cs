using Xunit;
using EntityGraphQL.Tests.ApiVersion1;

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
            Assert.True(provider.TypeHasField("location", "id"));
            Assert.True(provider.TypeHasField("location", "address"));
            Assert.True(provider.TypeHasField("location", "state"));
            Assert.True(provider.TypeHasField("location", "Country"));
            Assert.True(provider.TypeHasField("location", "planet"));
        }
        [Fact]
        public void ExposesDefinedFields()
        {
            var provider = new TestObjectGraphSchema();
            Assert.True(provider.TypeHasField("opentask", "id"));
            Assert.True(provider.TypeHasField("opentask", "assignee"));
            // Not exposed in our schema
            Assert.False(provider.TypeHasField("opentask", "IsActive"));
        }
        [Fact]
        public void ReturnsActualName()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("Id", schema.GetActualFieldName("project", "ID"));
            Assert.Equal("Name".ToLower(), schema.GetActualFieldName("project", "NAme").ToLower());
        }
    }
}