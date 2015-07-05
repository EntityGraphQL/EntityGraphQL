using Xunit;
using System.Collections.Generic;

namespace EntityQueryLanguage.Tests {
	/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
	public class ObjectSchemaProviderTests {
		[Fact]
		public void ReadsContextType() {
			var schema = new ObjectSchemaProvider(typeof(TestEntity));
			Assert.Equal(typeof(TestEntity), schema.ContextType);
		}
		[Fact]
		public void CachesPublicProperties() {
			var schema = new ObjectSchemaProvider(typeof(TestEntity));
			Assert.Equal(true, schema.EntityTypeHasField(typeof(TestEntity), "id"));
			Assert.Equal(true, schema.EntityTypeHasField(typeof(TestEntity), "Field1"));
			Assert.Equal(true, schema.EntityTypeHasField(typeof(TestEntity), "relation"));
			Assert.Equal(false, schema.EntityTypeHasField(typeof(TestEntity), "notthere"));
		}
		[Fact]
		public void CachesPublicFields() {
			var schema = new ObjectSchemaProvider(typeof(Person));
			Assert.Equal(true, schema.EntityTypeHasField(typeof(Person), "id"));
			Assert.Equal(true, schema.EntityTypeHasField(typeof(Person), "name"));
		}
		[Fact]
		public void ReturnsActualName() {
			var schema = new ObjectSchemaProvider(typeof(TestEntity));
			Assert.Equal("Id", schema.GetActualFieldName(typeof(TestEntity), "id"));
			Assert.Equal("Field1", schema.GetActualFieldName(typeof(TestEntity), "fiELd1"));
		}
		[Fact]
		public void CachesRecursively() {
			var schema = new ObjectSchemaProvider(typeof(TestSchema));
			Assert.Equal(true, schema.EntityTypeHasField(typeof(TestSchema), "someRelation"));
			Assert.Equal(true, schema.EntityTypeHasField(typeof(Person), "name"));
			Assert.Equal(true, schema.EntityTypeHasField(typeof(TestEntity), "field1"));
		}
		// This would be your Entity/Object graph you use with EntityFramework
		private class TestSchema {
			public TestEntity SomeRelation { get; }
			public IEnumerable<Person> People { get; }
		}
		
		private class TestEntity {
			public int Id { get; }
			public int Field1 { get; }
			public Person Relation { get; }
		}
		
		private class Person {
			public int Id = 0;
			public string Name = string.Empty;
		}
	}
}