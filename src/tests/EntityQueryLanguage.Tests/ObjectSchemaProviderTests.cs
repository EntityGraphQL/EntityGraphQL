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
            var schema = new ObjectSchemaProvider<TestEntity>();
            Assert.Equal(typeof(TestEntity), schema.ContextType);
        }
        [Fact]
        public void CachesPublicProperties()
        {
            var schema = new ObjectSchemaProvider<TestEntity>();
            Assert.Equal(true, schema.TypeHasField(typeof(TestEntity), "id"));
            Assert.Equal(true, schema.TypeHasField(typeof(TestEntity), "Field1"));
            Assert.Equal(true, schema.TypeHasField(typeof(TestEntity), "relation"));
            Assert.Equal(false, schema.TypeHasField(typeof(TestEntity), "notthere"));
        }
        [Fact]
        public void CachesPublicFields()
        {
            var schema = new ObjectSchemaProvider<Person>();
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "id"));
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "name"));
        }
        [Fact]
        public void ReturnsActualName()
        {
            var schema = new ObjectSchemaProvider<TestEntity>();
            Assert.Equal("Id", schema.GetActualFieldName(typeof(TestEntity).Name, "id"));
            Assert.Equal("Field1", schema.GetActualFieldName(typeof(TestEntity).Name, "fiELd1"));
        }
        [Fact]
        public void CachesRecursively()
        {
            var schema = new ObjectSchemaProvider<TestSchema>();
            Assert.Equal(true, schema.TypeHasField(typeof(TestSchema), "someRelation"));
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "name"));
            Assert.Equal(true, schema.TypeHasField(typeof(TestEntity), "field1"));
        }
        [Fact]
        public void AllowsExtending()
        {
            var schema = new ObjectSchemaProvider<TestSchema>();
            schema.ExtendType<Person>("idAndName", p => p.Id + " " + p.Name);
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "name"));
            Assert.Equal(true, schema.TypeHasField(typeof(Person), "idAndName"));
        }
        [Fact]
        public void CanNotOverrideExistingType()
        {
            var schema = new ObjectSchemaProvider<TestSchema>();
            var ex = Assert.Throws<ArgumentException>(() => {
                // Type "person" was auto created from the TestSchema
                schema.TypeFrom<Person>("person", description: "duplicate type", fields: new
                {
                    Id = schema.Field((Person p) => p.Id, "The unique identifier"),
                    FullName = schema.Field((Person p) => p.Name + " Fakey", "Person's full name")
                });
            });
            Assert.Equal("An item with the same key has already been added. Key: person", ex.Message);
        }
        [Fact]
        public void CanNotOverrideExistingType2()
        {
            var schema = new ObjectSchemaProvider<TestSchema>();
            var ex = Assert.Throws<ArgumentException>(() => {
                // Type "person" was auto created from the TestSchema
                schema.TypeFrom<Person>("person", description: "duplicate type", fields: new
                {
                    Count = schema.Field((Person c) => c.Name, "Total people")
                });
            });
            Assert.Equal("An item with the same key has already been added. Key: person", ex.Message);
        }
        // [Fact]
        // public void CanCreateAggregateType()
        // {
        //     var schema = new ObjectSchemaProvider<TestSchema>();
        //     schema.Type("Stats", description: "Some stats", fields: new
        //     {
        //         TotalPeople = schema.Field((TestSchema c) => c.Count(), "Total people")
        //     });
        // }
        // [Fact]
        // public void CanCreateNewTypeFromBaseType()
        // {
        //     var schema = new ObjectSchemaProvider<TestSchema>();
        //     schema.Type("BigPerson", query: ctx => ctx.People.Where(p => p.Height > 180), description: "Only big people", fields: new
        //     {
        //         Name = schema.Field((Person p) => p.Name, "Big person's name")
        //     });
        // }
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