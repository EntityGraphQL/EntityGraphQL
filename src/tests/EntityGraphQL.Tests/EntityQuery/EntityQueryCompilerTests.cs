using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using System;

namespace EntityGraphQL.Compiler.EntityQuery.Tests
{
    /// <summary>
    /// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
    /// </summary>
    public class EntityQueryCompilerTests
    {
        [Fact]
        public void CompilesNumberConstant()
        {
            var exp = EntityQueryCompiler.Compile("3");
            Assert.Equal((long)3, exp.Execute());
        }

        [Fact]
        public void CompilesNegitiveNumberConstant()
        {
            var exp = EntityQueryCompiler.Compile("-43");
            Assert.Equal((long)(-43), exp.Execute());
        }

        [Fact]
        public void CompilesNumberDecimalConstant()
        {
            var exp = EntityQueryCompiler.Compile("23.3");
            Assert.Equal(23.3m, exp.Execute());
        }

        [Fact]
        public void CompilesNullConstant()
        {
            var exp = EntityQueryCompiler.Compile("null");
            Assert.Null(exp.Execute());
        }

        [Fact]
        public void CompilesStringConstant()
        {
            var exp = EntityQueryCompiler.Compile("\"Hello there_987-%#&	;;s\"");
            Assert.Equal("Hello there_987-%#&	;;s", exp.Execute());
        }

        [Fact]
        public void CompilesStringConstant2()
        {
            var exp = EntityQueryCompiler.Compile("\"\"Hello\" there\"");
            Assert.Equal("\"Hello\" there", exp.Execute());
        }

        [Fact]
        public void CompilesIdentityCall()
        {
            var exp = EntityQueryCompiler.Compile("hello", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal("returned value", exp.Execute(new TestSchema()));
        }
        [Fact]
        public void FailsIdentityNotThere()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => EntityQueryCompiler.Compile("wrongField", SchemaBuilder.FromObject<TestSchema>()));
            Assert.Equal("Field wrongField not found on type Query", ex.Message);
        }
        [Fact]
        public void CompilesIdentityCallFullPath()
        {
            var exp = EntityQueryCompiler.Compile("someRelation.field1", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal(2, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesIdentityCallFullPathDeep()
        {
            var exp = EntityQueryCompiler.Compile("someRelation.relation.id", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal(99, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesBinaryExpressionEquals()
        {
            var exp = EntityQueryCompiler.Compile("someRelation.relation.id == 99", SchemaBuilder.FromObject<TestSchema>());
            Assert.True((bool)exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesBinaryExpressionPlus()
        {
            var exp = EntityQueryCompiler.Compile("someRelation.relation.id + 99", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal(198, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesBinaryExpressionEqualsAndAdd()
        {
            var exp = EntityQueryCompiler.Compile("someRelation.relation.id == (99 - 32)", SchemaBuilder.FromObject<TestSchema>());
            Assert.False((bool)exp.Execute(new TestSchema()));
        }

        [Fact]
        public void FailsIfThenElseInlineNoBrackets()
        {
            // no brackets so it reads it as someRelation.relation.id == (99 ? 'wooh' : 66) and fails as 99 is not a bool
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => EntityQueryCompiler.Compile("someRelation.relation.id == 99 ? \"wooh\" : 66", SchemaBuilder.FromObject<TestSchema>()));
            Assert.Equal("Expected boolean value in conditional test but found '99'", ex.Message);
        }

        [Fact]
        public void CompilesIfThenElseInlineTrueBrackets()
        {
            // tells it how to read it
            var exp = EntityQueryCompiler.Compile("(someRelation.relation.id == 99) ? 100 : 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((long)100, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesIfThenElseInlineFalseBrackets()
        {
            // tells it how to read it
            var exp = EntityQueryCompiler.Compile("(someRelation.relation.id == 98) ? 100 : 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((long)66, exp.Execute(new TestSchema()));
        }

        [Fact]
        public void CompilesIfThenElseTrue()
        {
            var exp = EntityQueryCompiler.Compile("if someRelation.relation.id == 99 then 100 else 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((long)100, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CompilesIfThenElseFalse()
        {
            var exp = EntityQueryCompiler.Compile("if someRelation.relation.id == 33 then 100 else 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((long)66, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CompilesBinaryWithIntAndUint()
        {
            var exp = EntityQueryCompiler.Compile("if someRelation.unisgnedInt == 33 then 100 else 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((long)66, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CompilesBinaryWithNullableAndNonNullable()
        {
            var exp = EntityQueryCompiler.Compile("if someRelation.nullableInt == 8 then 100 else 66", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal((long)100, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CompilesBinaryAnd()
        {
            var exp = EntityQueryCompiler.Compile("(someRelation.nullableInt == 9) && (hello == \"Hi\")", SchemaBuilder.FromObject<TestSchema>());
            Assert.Equal(false, exp.Execute(new TestSchema()));
        }
        [Fact]
        public void CanUseCompiledExpressionInWhereMethod()
        {
            var exp = EntityQueryCompiler.Compile("name == \"Bob\"", SchemaBuilder.FromObject<TestEntity>());
            var objects = new List<TestEntity> { new TestEntity("Sally"), new TestEntity("Bob") };
            Assert.Equal(2, objects.Count);
            var results = objects.Where((Func<TestEntity, bool>)exp.LambdaExpression.Compile());
            Assert.Single(results);
            Assert.Equal("Bob", results.ElementAt(0).Name);
        }

        [Fact]
        public void TestEntityQueryWorks()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestEntity>();
            var compiledResult = EntityQueryCompiler.Compile("(relation.id == 1) || (relation.id == 2)", schemaProvider);
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
            Assert.Equal(3, list.Count);
            var results = list.Where((Func<TestEntity, bool>)compiledResult.LambdaExpression.Compile());

            Assert.Equal(2, results.Count());
            Assert.Equal("bob", results.ElementAt(0).Name);
            Assert.Equal("mary", results.ElementAt(1).Name);
        }

        [Theory]
        [InlineData("\"2020-08-11T00:00:00\"")]
        [InlineData("\"2020-08-11 00:00:00\"")]
        [InlineData("\"2020-08-11\"")]
        public void TestEntityQueryWorksWithDates(string dateValue)
        {
            var schemaProvider = SchemaBuilder.FromObject<Entry>();
            schemaProvider.AddType<DateTime>("DateTime"); //<-- Tried with and without
            var compiledResult = EntityQueryCompiler.Compile($"when >= {dateValue}", schemaProvider);
            var list = new List<Entry> {
                new Entry("First") {
                    When = new DateTime(2020, 08, 10)
                },
                new Entry("Second") {
                    When = new DateTime(2020, 08, 11)
                },
                new Entry("Third") {
                    When = new DateTime(2020, 08, 12)
                }
            };
            Assert.Equal(3, list.Count());
            var results = list.Where((Func<Entry, bool>)compiledResult.LambdaExpression.Compile());

            Assert.Equal(2, results.Count());
            Assert.Equal("Second", results.ElementAt(0).Message);
            Assert.Equal("Third", results.ElementAt(1).Message);
        }

        [Theory]
        [InlineData("\"2020-08-11T13:22:11\"")]
        [InlineData("\"2020-08-11 13:22:11\"")]
        [InlineData("\"2020-08-11 13:22:11.1\"")]
        [InlineData("\"2020-08-11 13:22:11.3000003\"")]
        [InlineData("\"2020-08-11 13:22:11.3000003+000\"")]
        public void TestEntityQueryWorksWithDateTimes(string dateValue)
        {
            var schemaProvider = SchemaBuilder.FromObject<Entry>();
            schemaProvider.AddType<DateTime>("DateTime"); //<-- Tried with and without
            var compiledResult = EntityQueryCompiler.Compile($"when >= {dateValue}", schemaProvider);
            var list = new List<Entry> {
                new Entry("First") {
                    When = new DateTime(2020, 08, 10)
                },
                new Entry("Second") {
                    When = new DateTime(2020, 08, 11, 13, 21, 11)
                },
                new Entry("Third") {
                    When = new DateTime(2020, 08, 12, 13, 22, 11)
                }
            };
            Assert.Equal(3, list.Count());
            var results = list.Where((Func<Entry, bool>)compiledResult.LambdaExpression.Compile());

            Assert.Single(results);
            Assert.Equal("Third", results.ElementAt(0).Message);
        }

        [Fact]
        public void CompilesEnum()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            // schema.AddEnum("Gender", typeof(Gender), "My enum type");
            var exp = EntityQueryCompiler.Compile("people.where(gender == Female)", schema);
            var res = (IEnumerable<Person>)exp.Execute(new TestSchema());
            Assert.Empty(res);
        }

        [Fact]
        public void CompilesEnum2()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            var exp = EntityQueryCompiler.Compile("people.where(gender == Other)", schema);
            var res = (IEnumerable<Person>)exp.Execute(new TestSchema
            {
                People = new List<Person> {
                    new Person {
                        Gender = Gender.Female
                    },
                    new Person {
                        Gender = Gender.Other
                    }

                }
            });
            Assert.Single(res);
            Assert.Equal(Gender.Other, res.First().Gender);
        }

        [Fact]
        public void CompilesEnum3()
        {
            

            var schema = SchemaBuilder.FromObject<TestSchema>();
            var exp = EntityQueryCompiler.Compile("people.where(gender == Gender.Other)", schema);
            var res = (IEnumerable<Person>)exp.Execute(new TestSchema
            {
                People = new List<Person> {
                    new Person {                        
                        Gender = Gender.Female
                    },
                    new Person {
                        Gender = Gender.Other
                    }

                }
            });
            Assert.Single(res);
            Assert.Equal(Gender.Other, res.First().Gender);
        }

        [Fact]
        public void CompilesEnum4()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            schema.AddEnum("Size", typeof(Size), "");
            
            Assert.Throws<InvalidOperationException>(() =>
            {
                var exp = EntityQueryCompiler.Compile("people.where(gender == Size.Other)", schema);
            });
        }

        public enum Gender
        {
            Female,
            Male,
            Other
        }

        public enum Size
        {
            Small,
            Large,
            Other
        }

        private class Entry
        {
            public Entry(string message)
            {
                Message = message;
            }
            public DateTime When { get; set; }
            public string Message { get; set; }
        }

        // This would be your Entity/Object graph you use with EntityFramework
        private class TestSchema
        {
            public string Hello { get { return "returned value"; } }

            public TestEntity SomeRelation { get { return new TestEntity("bob"); } }
            public IEnumerable<Person> People { get; set; } = new List<Person>();
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
            public Gender Gender { get; set; }
        }
    }
}