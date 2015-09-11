using Xunit;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace EntityQueryLanguage.Tests {
	/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
	public class EqlCompilerTests {
		[Fact]
		public void CompilesNumberConstant() {
			var exp = EqlCompiler.Compile("3");
			Assert.Equal(3, exp.Execute());
		}

		[Fact]
		public void CompilesNegitiveNumberConstant() {
			var exp = EqlCompiler.Compile("-43");
			Assert.Equal(-43, exp.Execute());
		}
		
		[Fact]
		public void CompilesNumberDecimalConstant() {
			var exp = EqlCompiler.Compile("23.3");
			Assert.Equal(23.3m, exp.Execute());
		}
		
		[Fact]
		public void CompilesStringConstant() {
			var exp = EqlCompiler.Compile("'Hello there_987-%#&	;;s'");
			Assert.Equal("Hello there_987-%#&	;;s", exp.Execute());
		}
		
		[Fact]
		public void CompilesIdentityCall() {
			var exp = EqlCompiler.Compile("hello", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal("returned value", exp.Execute(new TestSchema()));
		}
		[Fact]
		public void FailsIdentityNotThere() {
			var ex = Assert.Throws<EqlCompilerException>(() => EqlCompiler.Compile("wrongField", new ObjectSchemaProvider(typeof(TestSchema))));
			Assert.Equal("Field or property 'wrongField' not found on current context 'TestSchema'", ex.Message);
		}
		[Fact]
		public void CompilesIdentityCallFullPath() {
			var exp = EqlCompiler.Compile("someRelation.field1", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(2, exp.Execute(new TestSchema()));
		}
		
		[Fact]
		public void CompilesIdentityCallFullPathDeep() {
			var exp = EqlCompiler.Compile("someRelation.relation.id", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(99, exp.Execute(new TestSchema()));
		}
		
		[Fact]
		public void CompilesBinaryExpressionEquals() {
			var exp = EqlCompiler.Compile("someRelation.relation.id = 99", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(true, exp.Execute(new TestSchema()));
		}
		
		[Fact]
		public void CompilesBinaryExpressionPlus() {
			var exp = EqlCompiler.Compile("someRelation.relation.id + 99", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(198, exp.Execute(new TestSchema()));
		}
		
		[Fact]
		public void CompilesBinaryExpressionEqualsAndAdd() {
			var exp = EqlCompiler.Compile("someRelation.relation.id = (99 - 32)", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(false, exp.Execute(new TestSchema()));
		}
		
		[Fact]
		public void FailsIfThenElseInlineNoBrackets() {
			// no brackets so it reads it as someRelation.relation.id = (99 ? 'wooh' : 66) and fails as 99 is not a bool
			var ex = Assert.Throws<EqlCompilerException>(() => EqlCompiler.Compile("someRelation.relation.id = 99 ? 'wooh' : 66", new ObjectSchemaProvider(typeof(TestSchema))));
			Assert.Equal("Expected boolean value in conditional test but found '99'", ex.Message);
		}
		
		[Fact]
		public void CompilesIfThenElseInlineTrueBrackets() {
			// tells it how to read it
			var exp = EqlCompiler.Compile("(someRelation.relation.id = 99) ? 100 : 66", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(100, exp.Execute(new TestSchema()));
		}
		
		[Fact]
		public void CompilesIfThenElseInlineFalseBrackets() {
			// tells it how to read it
			var exp = EqlCompiler.Compile("(someRelation.relation.id = 98) ? 100 : 66", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(66, exp.Execute(new TestSchema()));
		}
		
		[Fact]
		public void CompilesIfThenElseTrue() {
			var exp = EqlCompiler.Compile("if someRelation.relation.id = 99 then 100 else 66", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(100, exp.Execute(new TestSchema()));
		}
		[Fact]
		public void CompilesIfThenElseFalse() {
			var exp = EqlCompiler.Compile("if someRelation.relation.id = 33 then 100 else 66", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(66, exp.Execute(new TestSchema()));
		}
		[Fact]
		public void CompilesBinaryWithIntAndUint() {
			var exp = EqlCompiler.Compile("if someRelation.UnisgnedInt = 33 then 100 else 66", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(66, exp.Execute(new TestSchema()));
		}
		[Fact]
		public void CompilesBinaryWithNullableAndNonNullable() {
			var exp = EqlCompiler.Compile("if someRelation.NullableInt = 8 then 100 else 66", new ObjectSchemaProvider(typeof(TestSchema)));
			Assert.Equal(100, exp.Execute(new TestSchema()));
		}
		[Fact]
		public void CanUseCompiledExpressionInWhereMethod() {
			var exp = EqlCompiler.Compile("name = 'Bob", new ObjectSchemaProvider(typeof(TestEntity)));
			var objects = new List<TestEntity> { new TestEntity { Name = "Sally" }, new TestEntity { Name = "Bob" }};
			Assert.Equal(2, objects.Count);
			var results = objects.Where(exp.Expression);
			Assert.Equal(1, results.Count());
			Assert.Equal("Bob", results.ElementAt(0).Name);
		}
		
		// This would be your Entity/Object graph you use with EntityFramework
		private class TestSchema {
			public string Hello { get { return "returned value"; } }
			
			public TestEntity SomeRelation { get { return new TestEntity(); } }
			public IEnumerable<Person> People { get { return new List<Person>(); } }
		}
		
		private class TestEntity {
			public int Id { get { return 100; } }
			public int Field1 { get { return 2; } }
			public uint UnisgnedInt { get { return 2; } }
			public int? NullableInt { get { return 8; } }
			public string Name { get; set; }
			public Person Relation { get { return new Person(); } }
		}
		
		private class Person {
			public int Id { get { return 99; } }
			public string Name { get { return "Luke"; } }
		}
	}
}