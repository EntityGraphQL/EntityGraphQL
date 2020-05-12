using Xunit;
using System.Linq;
using System.Collections.Generic;
using System;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.LinqQuery.Tests
{
    public class DefaultMethodProviderTests
    {
        [Fact]
        public void CompilesFirst()
        {
            var exp = EntityQueryCompiler.Compile(@"people.first(guid = ""6492f5fe-0869-4279-88df-7f82f8e87a67"")", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null);
            var result = exp.Execute(new TestSchema()) as Person;
            Assert.Equal(new Guid("6492f5fe-0869-4279-88df-7f82f8e87a67"), result.Guid);
        }
        [Fact]
        public void CompilesWhere()
        {
            var exp = EntityQueryCompiler.Compile(@"people.where(name = ""bob"")", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null);
            var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
            Assert.Empty(result);
        }
        [Fact]
        public void CompilesWhere2()
        {
            var exp = EntityQueryCompiler.Compile(@"people.where(name = ""Luke"")", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null);
            var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
            Assert.Single(result);
        }
        [Fact]
        public void FailsWhereNoParameter()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => EntityQueryCompiler.Compile("people.where()", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null));
            Assert.Equal("Method 'where' expects 1 argument(s) but 0 were supplied", ex.Message);
        }
        [Fact]
        public void FailsWhereWrongParameterType()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => EntityQueryCompiler.Compile("people.where(name)", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null));
            Assert.Equal("Method 'where' expects parameter that evaluates to a 'System.Boolean' result but found result type 'System.String'", ex.Message);
        }

        [Fact]
        public void CompilesFirstWithPredicate()
        {
            var exp = EntityQueryCompiler.Compile(@"people.first(name = ""Luke"")", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null);
            var result = exp.Execute(new TestSchema()) as Person;
            Assert.Equal("Luke", result.Name);
        }
        [Fact]
        public void CompilesFirstNoPredicate()
        {
            var exp = EntityQueryCompiler.Compile("people.first()", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null);
            var result = exp.Execute(new TestSchema()) as Person;
            Assert.Equal("Bob", result.Name);
        }

        [Fact]
        public void CompilesTake()
        {
            var exp = EntityQueryCompiler.Compile("people.take(1)", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null);
            var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
            Assert.Single(result);
            Assert.Equal("Bob", result.ElementAt(0).Name);
        }
        [Fact]
        public void CompilesSkip()
        {
            var exp = EntityQueryCompiler.Compile("people.Skip(1)", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null);
            var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
            Assert.Equal(3, result.Count());
            Assert.Equal("Luke", result.ElementAt(0).Name);
        }

        [Fact]
        public void CompilesMethodsChained()
        {
            var exp = EntityQueryCompiler.Compile("people.where(id = 9).take(2)", SchemaBuilder.FromObject<TestSchema>(), null, new DefaultMethodProvider(), null);
            var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
            Assert.Equal(2, result.Count());
            Assert.Equal("Bob", result.ElementAt(0).Name);
            // should skip Luke because of the where
            Assert.Equal("Boba", result.ElementAt(1).Name);
        }

        // This would be your Entity/Object graph you use with EntityFramework
        private class TestSchema
        {
            public IEnumerable<Person> People
            {
                get
                {
                    return new List<Person> {
                    new Person{ Id = 9, Name = "Bob" },
                    new Person(),
                    new Person{ Id = 9, Name = "Boba" },
                    new Person{ Id = 9, Name = "Robin" },
                };
                }
            }
        }

        private class Person
        {
            public int Id { get; set; } = 99;
            public string Name { get; set; } = "Luke";
            public string LastName { get; set; } = "Lasty";
            public Guid Guid { get; set; } = new Guid("6492f5fe-0869-4279-88df-7f82f8e87a67");
        }
    }
}