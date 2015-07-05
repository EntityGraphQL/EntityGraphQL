using Xunit;

namespace EntityQueryLanguage.Tests {
	/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
	public class VersionSchemaProviderTests {
		//  [Fact]
		//  public void ReadsContextType() {
		//  	var schema = new ObjectSchemaProvider(typeof(Project));
		//  	Assert.Equal(typeof(Project), schema.ContextType);
		//  }
		//  [Fact]
		//  public void CachesPublicProperties() {
		//  	var schema = new ObjectSchemaProvider(typeof(Project));
		//  	Assert.Equal(true, schema.EntityTypeHasField(typeof(Project), "id"));
		//  	Assert.Equal(true, schema.EntityTypeHasField(typeof(Project), "Field1"));
		//  	Assert.Equal(true, schema.EntityTypeHasField(typeof(Project), "relation"));
		//  	Assert.Equal(false, schema.EntityTypeHasField(typeof(Project), "notthere"));
		//  }
		//  [Fact]
		//  public void CachesPublicFields() {
		//  	var schema = new ObjectSchemaProvider(typeof(Task));
		//  	Assert.Equal(true, schema.EntityTypeHasField(typeof(Task), "id"));
		//  	Assert.Equal(true, schema.EntityTypeHasField(typeof(Task), "name"));
		//  }
		//  [Fact]
		//  public void ReturnsActualName() {
		//  	var schema = new ObjectSchemaProvider(typeof(Project));
		//  	Assert.Equal("Id", schema.GetActualFieldName(typeof(Project), "id"));
		//  	Assert.Equal("Field1", schema.GetActualFieldName(typeof(Project), "fiELd1"));
		//  }
		//  [Fact]
		//  public void CachesRecursively() {
		//  	var schema = new ObjectSchemaProvider(typeof(TestSchema));
		//  	Assert.Equal(true, schema.EntityTypeHasField(typeof(TestSchema), "someRelation"));
		//  	Assert.Equal(true, schema.EntityTypeHasField(typeof(Task), "name"));
		//  	Assert.Equal(true, schema.EntityTypeHasField(typeof(Project), "field1"));
		//  }
	}
}