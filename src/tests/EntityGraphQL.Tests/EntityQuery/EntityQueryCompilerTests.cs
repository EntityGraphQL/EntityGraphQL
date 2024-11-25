using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery.Grammar;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Compiler.EntityQuery.Tests;

/// <summary>
/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
/// </summary>
public class EntityQueryCompilerTests
{
    private readonly ExecutionOptions executionOptions = new();

    [Fact]
    public void CompilesNumberConstant()
    {
        var exp = EntityQueryCompiler.Compile("3", executionOptions);
        Assert.Equal((long)3, exp.Execute());
    }

    [Fact]
    public void CompilesNegativeNumberConstant()
    {
        var exp = EntityQueryCompiler.Compile("-43", executionOptions);
        Assert.Equal((long)-43, exp.Execute());
    }

    [Fact]
    public void CompilesNumberDecimalConstant()
    {
        var exp = EntityQueryCompiler.Compile("23.3", executionOptions);
        Assert.Equal(23.3m, exp.Execute());
    }

    [Fact]
    public void CompilesNullConstant()
    {
        var exp = EntityQueryCompiler.Compile("null", executionOptions);
        Assert.Null(exp.Execute());
    }

    [Fact]
    public void CompilesStringConstant()
    {
        var exp = EntityQueryCompiler.Compile("\"Hello there_987-%#&	;;s\"", executionOptions);
        Assert.Equal("Hello there_987-%#&	;;s", exp.Execute());
    }

    [Fact]
    public void CompilesStringConstant2()
    {
        var exp = EntityQueryCompiler.Compile("\"\\\"Hello\\\" there\"", executionOptions);
        Assert.Equal("\"Hello\" there", exp.Execute());
    }

    [Fact]
    public void CompilesStringConstant3()
    {
        var exp = EntityQueryCompiler.Compile("\" \\\"\\n\\r\\0\\a\\b\\f\\t\\v \"", executionOptions);
        Assert.Equal(" \"\n\r\0\a\b\f\t\v ", exp.Execute());
    }

    [Fact]
    public void CompilesIdentityCall()
    {
        var exp = EntityQueryCompiler.Compile("hello", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal("returned value", exp.Execute(new TestSchema()));
    }

    [Fact]
    public void FailsIdentityNotThere()
    {
        var ex = Assert.Throws<EntityGraphQLCompilerException>(() => EntityQueryCompiler.Compile("wrongField", SchemaBuilder.FromObject<TestSchema>(), executionOptions));
        Assert.Equal("Field 'wrongField' not found on type 'Query'", ex.Message);
    }

    [Fact]
    public void CompilesIdentityCallFullPath()
    {
        var exp = EntityQueryCompiler.Compile("someRelation.field1", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal(2, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesIdentityCallFullPathDeep()
    {
        var exp = EntityQueryCompiler.Compile("someRelation.relation.id", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal(99, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesBinaryExpressionEquals()
    {
        var exp = EntityQueryCompiler.Compile("someRelation.relation.id == 99", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.True((bool)exp.Execute(new TestSchema())!);
    }

    [Fact]
    public void CompilesBinaryExpressionPlus()
    {
        var exp = EntityQueryCompiler.Compile("someRelation.relation.id + 99", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal(198, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesBinaryExpressionEqualsRoot()
    {
        var exp = EntityQueryCompiler.Compile("num == 34", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.False((bool)exp.Execute(new TestSchema())!);
    }

    [Fact]
    public void CompilesBinaryExpressionEqualsAndAddRoot()
    {
        var exp = EntityQueryCompiler.Compile("num == (90 - 57)", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.True((bool)exp.Execute(new TestSchema())!);
    }

    [Fact]
    public void CompilesBinaryExpressionEqualsAndAdd()
    {
        var exp = EntityQueryCompiler.Compile("someRelation.relation.id == (99 - 32)", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.False((bool)exp.Execute(new TestSchema())!);
    }

    [Fact]
    public void FailsIfThenElseInlineNoBrackets()
    {
        // no brackets so it reads it as someRelation.relation.id == (99 ? 'wooh' : 66) and fails as 99 is not a bool
        var ex = Assert.Throws<EntityGraphQLCompilerException>(
            () => EntityQueryCompiler.Compile("someRelation.relation.id == 99 ? \"wooh\" : 66", SchemaBuilder.FromObject<TestSchema>(), executionOptions)
        );
        Assert.Equal("Conditional result types mismatch. Types 'String' and 'Int64' must be the same.", ex.Message);
    }

    [Fact]
    public void CompilesIfThenElseInlineTrueBrackets()
    {
        // tells it how to read it
        var exp = EntityQueryCompiler.Compile("(someRelation.relation.id == 99) ? 100 : 66", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal((long)100, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesIfThenElseInlineFalseBrackets()
    {
        // tells it how to read it
        var exp = EntityQueryCompiler.Compile("(someRelation.relation.id == 98) ? 100 : 66", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal((long)66, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesIfThenElseTrue()
    {
        var exp = EntityQueryCompiler.Compile("if someRelation.relation.id == 99 then 100 else 66", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal((long)100, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesIfThenElseFalse()
    {
        var exp = EntityQueryCompiler.Compile("if someRelation.relation.id == 33 then 100 else 66", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal((long)66, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesBinaryWithIntAndUint()
    {
        var exp = EntityQueryCompiler.Compile("if someRelation.unisgnedInt == 33 then 100 else 66", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal((long)66, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesBinaryWithNullableAndNonNullable()
    {
        var exp = EntityQueryCompiler.Compile("if someRelation.nullableInt == 8 then 100 else 66", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal((long)100, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CompilesBinaryAnd()
    {
        var exp = EntityQueryCompiler.Compile("(someRelation.nullableInt == 9) && (hello == \"Hi\")", SchemaBuilder.FromObject<TestSchema>(), executionOptions);
        Assert.Equal(false, exp.Execute(new TestSchema()));
    }

    [Fact]
    public void CanUseCompiledExpressionInWhereMethod()
    {
        var exp = EntityQueryCompiler.Compile("name == \"Bob\"", SchemaBuilder.FromObject<TestEntity>(), executionOptions);
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
        var compiledResult = EntityQueryCompiler.Compile("(relation.id == 1) || (relation.id == 2)", schemaProvider, executionOptions);
        var list = new List<TestEntity>
        {
            new TestEntity("bob") { Relation = new Person { Id = 1 } },
            new TestEntity("mary") { Relation = new Person { Id = 2 } },
            new TestEntity("Jake") { Relation = new Person { Id = 5 } },
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
        var compiledResult = EntityQueryCompiler.Compile($"when >= {dateValue}", schemaProvider, executionOptions);
        var list = new List<Entry>
        {
            new("First") { When = new DateTime(2020, 08, 10) },
            new("Second") { When = new DateTime(2020, 08, 11) },
            new("Third") { When = new DateTime(2020, 08, 12) },
        };
        Assert.Equal(3, list.Count());
        var results = list.Where((Func<Entry, bool>)compiledResult.LambdaExpression.Compile());

        Assert.Equal(2, results.Count());
        Assert.Equal("Second", results.ElementAt(0).Message);
        Assert.Equal("Third", results.ElementAt(1).Message);
    }

    [Theory]
    [InlineData("\"3145-00-15T00:00:00\"")]
    [InlineData("\"2023-01-15T24:00:00\"")]
    [InlineData("\"2023-01-15T23:60:00\"")]
    [InlineData("\"2023-01-15T23:59:60\"")]
    public void TestEntityQueryFailsOnInvalidDate(string dateValue)
    {
        var schemaProvider = SchemaBuilder.FromObject<Entry>();
        schemaProvider.AddType<DateTime>("DateTime"); //<-- Tried with and without
        var compiledResult = EntityQueryCompiler.Compile($"when >= {dateValue}", schemaProvider, executionOptions);
        var list = new List<Entry> { new Entry("First") { When = new DateTime(2020, 08, 10) } };
        Assert.Single(list);
        var results = list.Where((Func<Entry, bool>)compiledResult.LambdaExpression.Compile());

        var exception = Assert.Throws<FormatException>(() => results.ToList());
        Assert.Equal($"The DateTime represented by the string '{dateValue.Trim('"')}' is not supported in calendar 'System.Globalization.GregorianCalendar'.", exception.Message);
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
        var compiledResult = EntityQueryCompiler.Compile($"when >= {dateValue}", schemaProvider, executionOptions);
        var list = new List<Entry>
        {
            new("First") { When = new DateTime(2020, 08, 10, 0, 0, 0) },
            new("Second") { When = new DateTime(2020, 08, 11, 13, 21, 11) },
            new("Third") { When = new DateTime(2020, 08, 12, 13, 22, 11) },
        };
        Assert.Equal(3, list.Count);
        var results = list.Where((Func<Entry, bool>)compiledResult.LambdaExpression.Compile());

        Assert.Single(results);
        Assert.Equal("Third", results.ElementAt(0).Message);
    }

    [Theory]
    // If <Offset> is missing, its default value is the offset of the local time zone. so running them locally will fail
    [InlineData("\"2020-08-11T13:22:11+0000\"", 1)]
    [InlineData("\"2020-08-11 13:22:11.3000003+0000\"", 1)]
    public void TestEntityQueryWorksWithDateTimeOffsets(string dateValue, int count)
    {
        var schemaProvider = SchemaBuilder.FromObject<Entry>();
        var compiledResult = EntityQueryCompiler.Compile($"whenOffset >= {dateValue}", schemaProvider, executionOptions);
        var list = new List<Entry>
        {
            new("First") { WhenOffset = new DateTimeOffset(2020, 08, 10, 0, 0, 0, TimeSpan.FromTicks(0)) },
            new("Second") { WhenOffset = new DateTimeOffset(2020, 08, 11, 13, 21, 11, TimeSpan.FromTicks(0)) },
            new("Third") { WhenOffset = new DateTimeOffset(2020, 08, 12, 13, 22, 11, TimeSpan.FromTicks(0)) },
        };
        Assert.Equal(3, list.Count);
        var filter = (Func<Entry, bool>)compiledResult.LambdaExpression.Compile();
        var results = list.Where(filter);

        Assert.Equal(count, results.Count());
        Assert.Equal("Third", results.Last().Message);
    }

    [Fact]
    public void CompilesEnumSimple()
    {
        var schema = SchemaBuilder.FromObject<TestSchema>();
        var param = Expression.Parameter(typeof(Person));
        var expressionParser = new EntityQueryParser(
            param,
            schema,
            new QueryRequestContext(null, null),
            new DefaultMethodProvider(),
            new CompileContext(executionOptions, null, new QueryRequestContext(null, null))
        );
        var exp = expressionParser.Parse("gender == Female");
        var res = (bool?)Expression.Lambda(exp, param).Compile().DynamicInvoke(new Person { Gender = Gender.Female });
        Assert.NotNull(res);
        Assert.True(res);
    }

    [Fact]
    public void CompilesEnum()
    {
        var schema = SchemaBuilder.FromObject<TestSchema>();
        var exp = EntityQueryCompiler.Compile("people.where(gender == Female)", schema, executionOptions);
        var res = (IEnumerable<Person>?)exp.Execute(new TestSchema());
        Assert.NotNull(res);
        Assert.Empty(res);
    }

    [Fact]
    public void CompilesEnum2()
    {
        var schema = SchemaBuilder.FromObject<TestSchema>();
        var exp = EntityQueryCompiler.Compile("people.where(gender == Other)", schema, executionOptions);
        var res = (IEnumerable<Person>?)
            exp.Execute(
                new TestSchema
                {
                    People = new List<Person>
                    {
                        new() { Gender = Gender.Female },
                        new() { Gender = Gender.Other },
                    },
                }
            );
        Assert.NotNull(res);
        Assert.Single(res);
        Assert.Equal(Gender.Other, res.First().Gender);
    }

    [Fact]
    public void CompilesEnum3()
    {
        var schema = SchemaBuilder.FromObject<TestSchema>();
        var exp = EntityQueryCompiler.Compile("people.where(gender == Gender.Other)", schema, executionOptions);
        var res = (IEnumerable<Person>?)
            exp.Execute(
                new TestSchema
                {
                    People = new List<Person>
                    {
                        new Person { Gender = Gender.Female },
                        new Person { Gender = Gender.Other },
                    },
                }
            );
        Assert.NotNull(res);
        Assert.Single(res);
        Assert.Equal(Gender.Other, res.First().Gender);
    }

    [Fact]
    public void CompilesEnum4()
    {
        var schema = SchemaBuilder.FromObject<TestSchema>();
        schema.AddEnum("Size", typeof(Size), "");

        Assert.Throws<EntityGraphQLCompilerException>(() =>
        {
            EntityQueryCompiler.Compile("people.where(gender == Size.Other)", schema, executionOptions);
        });
    }

    [Fact]
    public void CompilesConstantArray()
    {
        var schema = SchemaBuilder.FromObject<TestSchema>();

        var res = EntityQueryCompiler.Compile("[1, 4,5]", schema, executionOptions).Execute(new TestSchema());
        Assert.Collection((IEnumerable<long>)res!, i => Assert.Equal(1, i), i => Assert.Equal(4, i), i => Assert.Equal(5, i));
    }

    [Fact]
    public void CompilesConstantArrayString()
    {
        var schema = SchemaBuilder.FromObject<TestSchema>();

        var res = EntityQueryCompiler.Compile("[\"Hi\", \"World\"]", schema, executionOptions).Execute(new TestSchema());
        Assert.Collection((IEnumerable<string>)res!, i => Assert.Equal("Hi", i), i => Assert.Equal("World", i));
    }

    public enum Gender
    {
        Female,
        Male,
        Other,
    }

    public enum Size
    {
        Small,
        Large,
        Other,
    }

    private class Entry
    {
        public Entry(string message)
        {
            Message = message;
        }

        public DateTime When { get; set; }
        public DateTimeOffset WhenOffset { get; set; }
        public string Message { get; set; }
    }

    // This would be your Entity/Object graph you use with EntityFramework
    private class TestSchema
    {
        public string Hello => "returned value";
        public int Num => 33;

        public TestEntity SomeRelation => new TestEntity("bob");
        public IEnumerable<Person> People { get; set; } = new List<Person>();
    }

    private class TestEntity
    {
        public TestEntity(string name)
        {
            Name = name;
            Relation = new Person();
        }

        public int Id => 100;
        public int Field1 => 2;
        public uint UnisgnedInt => 2;
        public int? NullableInt => 8;
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
        public string Name => "Luke";
        public Gender Gender { get; set; }
    }
}
