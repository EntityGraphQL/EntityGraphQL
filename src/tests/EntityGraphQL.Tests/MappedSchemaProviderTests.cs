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
            Assert.True(provider.TypeHasField("location", "id", new string[0]));
            Assert.True(provider.TypeHasField("location", "address", new string[0]));
            Assert.True(provider.TypeHasField("location", "state", new string[0]));
            Assert.True(provider.TypeHasField("location", "Country", new string[0]));
            Assert.True(provider.TypeHasField("location", "planet", new string[0]));
        }
        [Fact]
        public void ExposesDefinedFields()
        {
            var provider = new TestObjectGraphSchema();
            Assert.True(provider.TypeHasField("opentask", "id", new string[0]));
            Assert.True(provider.TypeHasField("opentask", "assignee", new string[0]));
            // Not exposed in our schema
            Assert.False(provider.TypeHasField("opentask", "IsActive", new string[0]));
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