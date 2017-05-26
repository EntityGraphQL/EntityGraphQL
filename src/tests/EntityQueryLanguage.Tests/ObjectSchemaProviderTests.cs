using Xunit;
using System.Collections.Generic;
using System;

namespace EntityQueryLanguage.Tests
{
    /// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
    public class ObjectSchemaProviderTests
    {
        [Fact]
        public void ReadsContextType()
        {
            var schema = SchemaBuilder.FromObject<TestEntity>();
            Assert.Equal(typeof(TestEntity), schema.ContextType);
        }
        [Fact]
        public void CachesPublicProperties()
        {
            var schema = SchemaBuilder.FromObject<TestEntity>();
            Assert.Equal(true, schema.TypeHasField(typeof(TestEntity), "id"));
            Assert.Equal(true, schema.TypeHasField(typeof(TestEntity), "Field1"));
            Assert.Equal(true, schema.TypeHasField(typeof(TestEntity), "relation"));
            Assert.Equal(false, schema.TypeHasField(typeof(TestEntity), "notthere"));
        }
        [Fact]
        public void CachesPublicFields()
        {
            var schema = SchemaBuilder.FromObject<Person>();
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "id"));
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "name"));
        }
        [Fact]
        public void ReturnsActualName()
        {
            var schema = SchemaBuilder.FromObject<TestEntity>();
            Assert.Equal("Id", schema.GetActualFieldName(typeof(TestEntity).Name, "id"));
            Assert.Equal("Field1", schema.GetActualFieldName(typeof(TestEntity).Name, "fiELd1"));
        }
        [Fact]
        public void CachesRecursively()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            Assert.Equal(true, schema.TypeHasField(typeof(TestSchema), "someRelation"));
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "name"));
            Assert.Equal(true, schema.TypeHasField(typeof(TestEntity), "field1"));
        }
        [Fact]
        public void AllowsExtending()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            schema.Type<Person>().AddField("idAndName", p => p.Id + " " + p.Name, "The Id and Name");
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "name"));
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "idAndName"));
        }
        [Fact]
        public void CanNotOverrideExistingType()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            var ex = Assert.Throws<ArgumentException>(() => {
                // Type "person" was auto created from the TestSchema
                var t = schema.AddType<Person>("person", description: "duplicate type");
                t.AddField(p => p.Id, "The unique identifier");
                t.AddField(p => p.Name + " Fakey", "Person's full name");
            });
            Assert.Equal("An item with the same key has already been added. Key: person", ex.Message);
        }
        // This would be your Entity/Object graph you use with EntityFramework
        private class TestSchema
        {
            public TestEntity SomeRelation { get; }
            public IEnumerable<Person> People { get; }
        }

        private class TestEntity
        {
            public int Id { get; }
            public int Field1 { get; }
            public Person Relation { get; }
        }

        private class Person
        {
            public int Id = 0;
            public string Name = string.Empty;
        }
    }
}