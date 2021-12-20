using Xunit;
using System.Collections.Generic;
using System;
using EntityGraphQL.Schema;
using System.Linq;

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
            Assert.True(schema.TypeHasField(typeof(TestEntity), "id", Array.Empty<string>(), null));
            Assert.True(schema.TypeHasField(typeof(TestEntity), "field1", Array.Empty<string>(), null));
            Assert.True(schema.TypeHasField(typeof(TestEntity), "relation", Array.Empty<string>(), null));
            Assert.False(schema.TypeHasField(typeof(TestEntity), "notthere", Array.Empty<string>(), null));
        }
        [Fact]
        public void CachesPublicFields()
        {
            var schema = SchemaBuilder.FromObject<Person>();
            Assert.True(schema.TypeHasField(typeof(Person), "id", Array.Empty<string>(), null));
            Assert.True(schema.TypeHasField(typeof(Person), "name", Array.Empty<string>(), null));
        }
        [Fact]
        public void CachesRecursively()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            Assert.True(schema.TypeHasField(typeof(TestSchema), "someRelation", Array.Empty<string>(), null));
            Assert.True(schema.TypeHasField(typeof(Person), "name", Array.Empty<string>(), null));
            Assert.True(schema.TypeHasField(typeof(TestEntity), "field1", Array.Empty<string>(), null));
        }
        [Fact]
        public void AllowsExtending()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            schema.Type<Person>().AddField("idAndName", p => p.Id + " " + p.Name, "The Id and Name");
            Assert.True(schema.TypeHasField(typeof(Person), "name", Array.Empty<string>(), null));
            Assert.True(schema.TypeHasField(typeof(Person), "idAndName", Array.Empty<string>(), null));
        }
        [Fact]
        public void CanNotOverrideExistingType()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                // Type "person" was auto created from the TestSchema
                var t = schema.AddType<Person>("Person", description: "duplicate type");
                t.AddField(p => p.Id, "The unique identifier");
                t.AddField(p => p.Name + " Fakey", "Person's full name");
            });
            Assert.Equal("An item with the same key has already been added. Key: Person", ex.Message);
        }

        [Fact]
        public void AutoAddArgumentForId()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            var argumentTypes = schema.Type<TestSchema>().GetField("person", null).Arguments;
            Assert.Single(argumentTypes);
            Assert.Equal("id", argumentTypes.First().Key);
            Assert.Equal(typeof(int), argumentTypes.First().Value.Type.TypeDotnet);
            Assert.True(argumentTypes.First().Value.Type.TypeNotNullable);
        }
        [Fact]
        public void AutoAddArgumentForIdGuid()
        {
            var schema = SchemaBuilder.FromObject<TestSchema2>();
            var argumentTypes = schema.Type<TestSchema2>().GetField("property", null).Arguments;
            Assert.Single(argumentTypes);
            Assert.Equal("id", argumentTypes.First().Key);
            Assert.Equal(typeof(Guid), argumentTypes.First().Value.Type.TypeDotnet);
            Assert.True(argumentTypes.First().Value.Type.TypeNotNullable);
        }
        [Fact]
        public void DoesNotSupportSameFieldDifferentArguments()
        {
            // Grpahql doesn't support "field overloading"
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(true);
            // user(id: ID) already created
            var ex = Assert.Throws<EntityQuerySchemaException>(() => schemaProvider.AddField("people", new { monkey = ArgumentHelper.Required<int>() }, (ctx, param) => ctx.People.Where(u => u.Id == param.monkey).FirstOrDefault(), "Return a user by ID"));
            Assert.Equal("Field people already exists on type RootQuery. Use ReplaceField() if this is intended.", ex.Message);
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