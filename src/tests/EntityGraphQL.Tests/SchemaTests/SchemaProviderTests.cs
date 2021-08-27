using Xunit;
using EntityGraphQL.Tests.ApiVersion1;
using System.Linq;

namespace EntityGraphQL.Tests
{
    public class SchemaProviderTests
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
            Assert.True(provider.TypeHasField("Location", "id", new string[0], null));
            Assert.True(provider.TypeHasField("Location", "address", new string[0], null));
            Assert.True(provider.TypeHasField("Location", "state", new string[0], null));
            Assert.True(provider.TypeHasField("Location", "country", new string[0], null));
            Assert.True(provider.TypeHasField("Location", "planet", new string[0], null));
        }
        [Fact]
        public void ExposesDefinedFields()
        {
            var provider = new TestObjectGraphSchema();
            Assert.True(provider.TypeHasField("Person", "id", new string[0], null));
            Assert.True(provider.TypeHasField("Person", "name", new string[0], null));
            // Not exposed in our schema
            Assert.True(provider.TypeHasField("Person", "fullName", new string[0], null));
        }
        [Fact]
        public void ReturnsActualName()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("id", schema.GetActualField("Project", "id", null).Name);
            Assert.Equal("name", schema.GetActualField("Project", "name", null).Name);
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
            Assert.Equal("id", schema.GetActualField("Project", "id", null).Name);
            schema.RemoveTypeAndAllFields<Project>();
            Assert.Empty(schema.GetQueryFields().Where(s => s.ReturnType.SchemaType.Name == "project"));
        }
        [Fact]
        public void RemovesTypeAndFields2()
        {
            var schema = new TestObjectGraphSchema();
            Assert.Equal("id", schema.GetActualField("Project", "id", null).Name);
            schema.RemoveTypeAndAllFields("Project");
            Assert.Empty(schema.GetQueryFields().Where(s => s.ReturnType.SchemaType.Name == "project"));
        }
    }
}