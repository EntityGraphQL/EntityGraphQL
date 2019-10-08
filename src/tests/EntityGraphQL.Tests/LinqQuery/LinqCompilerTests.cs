using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Extensions;
using EntityGraphQL.Compiler;
using System;

namespace EntityGraphQL.LinqQuery.Tests
{
    /// <summary>
    /// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
    /// </summary>
    public class LinqCompilerTests
    {
        [Fact]
        public void CompilesNumberConstant()
        {
            var exp = EqlCompiler.Compile("3");
            Assert.Equal((UInt64)3, exp.Execute());
        }

        [Fact]
        public void CompilesNegitiveNumberConstant()
        {
            var exp = EqlCompiler.Compile("-43");
            Assert.Equal((Int64)(-43), exp.Execute());
        }

        [Fact]
        public void CompilesNumberDecimalConstant()
        {
            var exp = EqlCompiler.Compile("23.3");
            Assert.Equal(23.3m, exp.Execute());
        }

        [Fact]
        public void CompilesNullConstant()
        {
            var exp = EqlCompiler.Compile("null");
            Assert.Null(exp.Execute());
        }

        [Fact]
        public void CompilesEmptyConstant()
        {
            var exp = EqlCompiler.Compile("empty");
            Assert.Null(exp.Execute());
        }

        [Fact]
        public void CompilesStringConstant()
        {
            var exp = EqlCompiler.Compile("\"Hello there_987-%#&	;;s\"");
            Assert.Equal("Hello there_987-%#&	;;s", exp.Execute());
        }

        [Fact]
        public void CompilesStringConstant2()
        {
            var exp = EqlCompiler.Compile("\"\"Hello\" there\"");
            Assert.Equal("\"Hello\" there", exp.Execute());
        }

        [Fact]
        public void CompilesIdentityCall()
        {
            var exp = EqlCompiler.Compile("hello", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal("returned value", exp.Execute(new TestSchema()));
        }
        [Fact]
        public void FailsIdentityNotThere()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => EqlCompiler.Compile("wrongField", SchemaBuilder.FromObject<TestSchema>()));
            Assert.Equal("Field 'wrongField' not found on current context 'TestSchema'", ex.Message);
        }
        [Fact]
        public void CompilesIdentityCallFullPath()
        {
            var exp = EqlCompiler.Compile("someRelation.field1", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal(2, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesIdentityCallFullPathDeep()
        {
            var exp = EqlCompiler.Compile("someRelation.relation.id", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal(99, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesBinaryExpressionEquals()
        {
            var exp = EqlCompiler.Compile("someRelation.relation.id = 99", SchemaBuilder.FromObject<TestSchema>());
            Assert.True((bool)exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesBinaryExpressionPlus()
        {
            var exp = EqlCompiler.Compile("someRelation.relation.id + 99", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal(198, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesBinaryExpressionEqualsAndAdd()
        {
            var exp = EqlCompiler.Compile("someRelation.relation.id = (99 - 32)", SchemaBuilder.FromObject<TestSchema>());
            Assert.False((bool)exp.Execute(new TestSchema()));
        }

        [Fact]
        public void FailsIfThenElseInlineNoBrackets()
        {
            // no brackets so it reads it as someRelation.relation.id = (99 ? 'wooh' : 66) and fails as 99 is not a bool
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => EqlCompiler.Compile("someRelation.relation.id = 99 ? \"wooh\" : 66", SchemaBuilder.FromObject<TestSchema>()));
            Assert.Equal("Expected boolean value in conditional test but found '99'", ex.Message);
        }

        [Fact]
        public void CompilesIfThenElseInlineTrueBrackets()
        {
            // tells it how to read it
            var exp = EqlCompiler.Compile("(someRelation.relation.id = 99) ? 100 : 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((UInt64)100, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesIfThenElseInlineFalseBrackets()
        {
            // tells it how to read it
            var exp = EqlCompiler.Compile("(someRelation.relation.id = 98) ? 100 : 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((UInt64)66, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesIfThenElseTrue()
        {
            var exp = EqlCompiler.Compile("if someRelation.relation.id = 99 then 100 else 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((UInt64)100, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CompilesIfThenElseFalse()
        {
            var exp = EqlCompiler.Compile("if someRelation.relation.id = 33 then 100 else 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((UInt64)66, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CompilesBinaryWithIntAndUint()
        {
            var exp = EqlCompiler.Compile("if someRelation.unisgnedInt = 33 then 100 else 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((UInt64)66, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CompilesBinaryWithNullableAndNonNullable()
        {
            var exp = EqlCompiler.Compile("if someRelation.nullableInt = 8 then 100 else 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((UInt64)100, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CanUseCompiledExpressionInWhereMethod()
        {
            var exp = EqlCompiler.Compile("name = \"Bob\"", SchemaBuilder.FromObject<TestEntity>());
            var objects = new List<TestEntity> { new TestEntity("Sally"), new TestEntity("Bob") };
            Assert.Equal(2, objects.Count);
            var results = objects.Where(exp.LambdaExpression);
            Assert.Single(results);
            Assert.Equal("Bob", results.ElementAt(0).Name);
        }

        [Fact]
        public void TestLinqQueryWorks()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestEntity>();
            var compiledResult = EqlCompiler.Compile("(relation.id = 1) or (relation.id = 2)", schemaProvider);
            var list = new List<TestEntity> {
                new TestEntity("bob") {
                    Relation = new Person {
                        Id = 1
                    }
                },
                new TestEntity("mary") {
                    Relation = new Person {
                        Id = 2
                    }
                },
                new TestEntity("Jake") {
                    Relation = new Person {
                        Id = 5
                    }
                }
            };
            Assert.Equal(3, list.Count());
            var results = list.Where(compiledResult.LambdaExpression);

            Assert.Equal(2, results.Count());
            Assert.Equal("bob", results.ElementAt(0).Name);
            Assert.Equal("mary", results.ElementAt(1).Name);
        }

        // This would be your Entity/Object graph you use with EntityFramework
        private class TestSchema
        {
            public string Hello { get { return "returned value"; } }

            public TestEntity SomeRelation { get { return new TestEntity("bob"); } }
            public IEnumerable<Person> People { get { return new List<Person>(); } }
        }

        private class TestEntity
        {
            public TestEntity(string name)
            {
                Name = name;
                Relation = new Person();
            }

            public int Id { get { return 100; } }
            public int Field1 { get { return 2; } }
            public uint UnisgnedInt { get { return 2; } }
            public int? NullableInt { get { return 8; } }
            public string Name { get; set; }
            public Person Relation { get; set; }
        }

        private class Person
        {
            public Person()
            {
                Id = 99;
            }
            public int Id { get; set; }
            public string Name { get { return "Luke"; } }
        }
    }
}