using Xunit;
using EntityQueryLanguage.Tests.ApiVersion1;

namespace EntityQueryLanguage.Tests
{
    /// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
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
            Assert.Equal(true, provider.TypeHasField("location", "id"));
            Assert.Equal(true, provider.TypeHasField("location", "address"));
            Assert.Equal(true, provider.TypeHasField("location", "state"));
            Assert.Equal(true, provider.TypeHasField("location", "Country"));
            Assert.Equal(true, provider.TypeHasField("location", "planet"));
        }
        [Fact]
        public void ExposesDefinedFields()
        {
            var provider = new TestObjectGraphSchema();
            Assert.Equal(true, provider.TypeHasField("opentask", "id"));
            Assert.Equal(true, provider.TypeHasField("opentask", "description"));
            // Name is not exposed in our schema
            Assert.Equal(false, provider.TypeHasField("opentask", "name"));
        }
        [Fact]
        public void ReturnsActualName()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("Id", schema.GetActualFieldName("project", "ID"));
            Assert.Equal("Name", schema.GetActualFieldName("project", "NAme"));
        }
    }
}