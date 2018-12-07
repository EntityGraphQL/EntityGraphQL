using Xunit;
using System.Collections.Generic;
using System;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Tests
{
    /// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
    public class SchemaBuilderTests
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
            Assert.True(schema.TypeHasField(typeof(TestEntity), "id", new string[0]));
            Assert.True(schema.TypeHasField(typeof(TestEntity), "Field1", new string[0]));
            Assert.True(schema.TypeHasField(typeof(TestEntity), "relation", new string[0]));
            Assert.False(schema.TypeHasField(typeof(TestEntity), "notthere", new string[0]));
        }
        [Fact]
        public void CachesPublicFields()
        {
            var schema = SchemaBuilder.FromObject<Person>();
            Assert.True(schema.TypeHasField(typeof(Person), "id", new string[0]));
            Assert.True(schema.TypeHasField(typeof(Person), "name", new string[0]));
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
            Assert.True(schema.TypeHasField(typeof(TestSchema), "someRelation", new string[0]));
            Assert.True(schema.TypeHasField(typeof(Person), "name", new string[0]));
            Assert.True(schema.TypeHasField(typeof(TestEntity), "field1", new string[0]));
        }
        [Fact]
        public void AllowsExtending()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            schema.Type<Person>().AddField("idAndName", p => p.Id + " " + p.Name, "The Id and Name");
            Assert.True(schema.TypeHasField(typeof(Person), "name", new string[0]));
            Assert.True(schema.TypeHasField(typeof(Person), "idAndName", new string[0]));
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

        [Fact]
        public void AutoAddArgumentForId()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            var argumentTypes = schema.Type<TestSchema>().GetField("person", "id").ArgumentTypes.GetType();
            Assert.Single(argumentTypes.GetFields());
            var prop = argumentTypes.GetFields()[0];
            Assert.Equal("id", prop.Name);
            Assert.Equal(typeof(RequiredField<int>), prop.FieldType);
        }
        [Fact]
        public void AutoAddArgumentForIdGuid()
        {
            var schema = SchemaBuilder.FromObject<TestSchema2>();
            var argumentTypes = schema.Type<TestSchema2>().GetField("property", "id").ArgumentTypes.GetType();
            Assert.Single(argumentTypes.GetFields());
            var prop = argumentTypes.GetFields()[0];
            Assert.Equal("id", prop.Name);
            Assert.Equal(typeof(RequiredField<Guid>), prop.FieldType);
        }
        // This would be your Entity/Object graph you use with EntityFramework
        private class TestSchema
        {
            public TestEntity SomeRelation { get; }
            public IEnumerable<Person> People { get; }
        }

        private class TestSchema2
        {
            public IEnumerable<Property> Properties { get; }
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

        private class Property
        {
            public Guid Id { get; set; }
        }
    }
}