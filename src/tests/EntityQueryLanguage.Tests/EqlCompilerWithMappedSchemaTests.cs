using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityQueryLanguage.Tests.ApiVersion1;

namespace EntityQueryLanguage.Tests {
	/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
	public class EqlCompilerWithMappedSchemaTests {
		[Fact]
		public void CompilesIdentityCall() {
			var exp = EqlCompiler.Compile("people", new TestObjectGraphSchema());
			dynamic result = exp.Execute(GetDataContext());
			Assert.Equal(1, Enumerable.Count(result));
		}
		[Fact]
		public void CompilesIdentityCallFullPath() {
			var exp = EqlCompiler.Compile("privateProjects.where(id = 8).count()", new TestObjectGraphSchema());
			Assert.Equal(0, exp.Execute(GetDataContext()));
			var exp2 = EqlCompiler.Compile("privateProjects.count()", new TestObjectGraphSchema());
			Assert.Equal(1, exp2.Execute(GetDataContext()));
		}
		[Fact]
		public void CompilesTypeBuiltFromObject() {
			// no brackets so it reads it as someRelation.relation.id = (99 ? 'wooh' : 66) and fails as 99 is not a bool
			var exp = EqlCompiler.Compile("defaultlocation.id = 10", new TestObjectGraphSchema());
			Assert.Equal(true, exp.Execute(GetDataContext()));
		}
		[Fact]
		public void CompilesIfThenElseInlineFalseBrackets() {
			var exp = EqlCompiler.Compile("(publicProjects.Count(id = 90) = 1) ? 'Yes' : 'No'", new TestObjectGraphSchema());
			Assert.Equal("Yes", exp.Execute(GetDataContext()));
		}
		[Fact]
		public void CompilesIfThenElseTrue() {
			var exp = EqlCompiler.Compile("if publicProjects.Count() > 1 then 'Yes' else 'No'", new TestObjectGraphSchema());
			Assert.Equal("No", exp.Execute(GetDataContext()));
		}

		private TestDataContext GetDataContext() {
			var db = new TestDataContext();
			db.Projects = new List<Project> { new Project { Id = 90, Type = 2 }, new Project { Id = 91, Type = 1 } };
			db.People = new List<Person> { new Person { Id = 4 } };
			db.Locations = new List<Location> { new Location { Id = 10 } };
			return db;
		}
	}
}