using Xunit;
using System.Linq;
using System.Collections.Generic;

namespace EntityQueryLanguage.Tests {
	public class DefaultMethodProviderTests {
		[Fact]
		public void CompilesWhere() {
			var exp = EqlCompiler.Compile("people.where(name = 'bob')", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider());
			var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
			Assert.Equal(0, result.Count());
		}
		[Fact]
		public void CompilesWhere2() {
			var exp = EqlCompiler.Compile("people.where(name = 'Luke')", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider());
			var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
			Assert.Equal(1, result.Count());
		}
		[Fact]
		public void FailsWhereNoParameter() {
			var ex = Assert.Throws<EqlCompilerException>(() => EqlCompiler.Compile("people.where()", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider()));
			Assert.Equal("Method 'where' expects 1 argument(s) but 0 were supplied", ex.Message);
		}
		[Fact]
		public void FailsWhereWrongParameterType() {
			var ex = Assert.Throws<EqlCompilerException>(() => EqlCompiler.Compile("people.where(name)", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider()));
			Assert.Equal("Method 'where' expects parameter that evaluates to a 'System.Boolean' result but found result type 'System.String'", ex.Message);
		}
		
		[Fact]
		public void CompilesFirstWithPredicate() {
			var exp = EqlCompiler.Compile("people.first(name = 'Luke')", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider());
			var result = exp.Execute(new TestSchema()) as Person;
			Assert.Equal("Luke", result.Name);
		}
		[Fact]
		public void CompilesFirstNoPredicate() {
			var exp = EqlCompiler.Compile("people.first()", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider());
			var result = exp.Execute(new TestSchema()) as Person;
			Assert.Equal("Bob", result.Name);
		}
		
		[Fact]
		public void CompilesTake() {
			var exp = EqlCompiler.Compile("people.take(1)", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider());
			var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
			Assert.Equal(1, result.Count());
			Assert.Equal("Bob", result.ElementAt(0).Name);
		}
		[Fact]
		public void CompilesSkip() {
			var exp = EqlCompiler.Compile("people.Skip(1)", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider());
			var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
			Assert.Equal(3, result.Count());
			Assert.Equal("Luke", result.ElementAt(0).Name);
		}
		
		[Fact]
		public void CompilesMethodsChained() {
			var exp = EqlCompiler.Compile("people.where(id = 9).take(2)", new ObjectSchemaProvider(typeof(TestSchema)), new DefaultMethodProvider());
			var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
			Assert.Equal(2, result.Count());
			Assert.Equal("Bob", result.ElementAt(0).Name);
			// should skip Luke because of the where
			Assert.Equal("Boba", result.ElementAt(1).Name);
		}
		
		// This would be your Entity/Object graph you use with EntityFramework
		private class TestSchema {
			public IEnumerable<Person> People {
				get { return new List<Person> {
					new Person{ Id = 9, Name = "Bob" },
					new Person(),
					new Person{ Id = 9, Name = "Boba" },
					new Person{ Id = 9, Name = "Robin" },
				}; }
			}
		}
		
		private class Person {
			public int Id { get; set; } = 99;
			public string Name { get; set; } = "Luke";
			public string LastName { get; set; } = "Lasty";
		}
	}
}