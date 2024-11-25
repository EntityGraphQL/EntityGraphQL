using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Compiler.EntityQuery.Tests;

public class DefaultMethodProviderTests
{
    private readonly ExecutionOptions executionOptions = new();

    [Fact]
    public void CompilesFirst()
    {
        var exp = EntityQueryCompiler.Compile(@"people.first(guid == ""6492f5fe-0869-4279-88df-7f82f8e87a67"")", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as Person;
        Assert.NotNull(result);
        Assert.Equal(new Guid("6492f5fe-0869-4279-88df-7f82f8e87a67"), result.Guid);
    }

    [Fact]
    public void CompilesWhere()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(name == ""bob"")", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void CompilesWhere2()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(name == ""Luke"")", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public void FailsWhereNoParameter()
    {
        var ex = Assert.Throws<EntityGraphQLCompilerException>(
            () => EntityQueryCompiler.Compile("people.where()", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider())
        );
        Assert.Equal("Method 'where' expects 1 argument(s) but 0 were supplied", ex.Message);
    }

    [Fact]
    public void FailsWhereWrongParameterType()
    {
        var ex = Assert.Throws<EntityGraphQLCompilerException>(
            () => EntityQueryCompiler.Compile("people.where(name)", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider())
        );
        Assert.Equal("Method 'where' expects parameter that evaluates to a 'System.Boolean' result but found result type 'System.String'", ex.Message);
    }

    [Fact]
    public void CompilesFirstWithPredicate()
    {
        var exp = EntityQueryCompiler.Compile(@"people.first(name == ""Luke"")", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as Person;
        Assert.NotNull(result);
        Assert.Equal("Luke", result.Name);
    }

    [Fact]
    public void CompilesFirstNoPredicate()
    {
        var exp = EntityQueryCompiler.Compile("people.first()", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as Person;
        Assert.NotNull(result);
        Assert.Equal("Bob", result.Name);
    }

    [Fact]
    public void CompilesTake()
    {
        var context = new TestSchema();
        var exp = EntityQueryCompiler.Compile("people.take(1)", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(context) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.True(context.People.Count() > 1);
        Assert.Equal("Bob", result.ElementAt(0).Name);
        Assert.Single(result);
    }

    [Fact]
    public void CompilesSkip()
    {
        var context = new TestSchema();
        var exp = EntityQueryCompiler.Compile("people.Skip(1)", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(context) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
        Assert.NotEqual("Luke", context.People.ElementAt(0).Name);
        Assert.Equal("Luke", context.People.ElementAt(1).Name);
        Assert.Equal("Luke", result.ElementAt(0).Name);
    }

    [Fact]
    public void CompilesMethodsChained()
    {
        var exp = EntityQueryCompiler.Compile("people.where(id == 9).take(2)", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Equal("Bob", result.ElementAt(0).Name);
        // should skip Luke because of the where
        Assert.Equal("Boba", result.ElementAt(1).Name);
    }

    [Fact]
    public void CompilesStringContains()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(name.contains(""ob""))", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
        Assert.Equal("Bob", result.ElementAt(0).Name);
        Assert.Equal("Boba", result.ElementAt(1).Name);
        Assert.Equal("Robin", result.ElementAt(2).Name);
    }

    [Fact]
    public void CompilesStringStartsWith()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(name.startsWith(""Bo""))", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Equal("Bob", result.ElementAt(0).Name);
        Assert.Equal("Boba", result.ElementAt(1).Name);
    }

    [Fact]
    public void CompilesStringEndsWith()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(name.endsWith(""b""))", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Bob", result.ElementAt(0).Name);
    }

    [Fact]
    public void CompilesStringToLower()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(name.toLower() == ""bob"")", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Bob", result.ElementAt(0).Name);
    }

    [Fact]
    public void CompilesStringToUpper()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(name.toUpper() == ""BOB"")", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Bob", result.ElementAt(0).Name);
    }

    [Fact]
    public void CompilesAndConvertsStringToGuid()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(guid == ""6492f5fe-0869-4279-88df-7f82f8e87a67"")", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var result = exp.Execute(new TestSchema()) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Luke", result.ElementAt(0).Name);
    }

    [Fact]
    public void SupportUseFilterIsAnyMethod()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(name.isAny([""Bob"", ""Robin""]))", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var data = new TestSchema();
        var result = exp.Execute(data) as IEnumerable<Person>;
        Assert.True(data.People.Count() > 2);
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Equal("Bob", result.ElementAt(0).Name);
        Assert.Equal("Robin", result.ElementAt(1).Name);
    }

    [Fact]
    public void SupportUseFilterIsAnyMethodOnNullable()
    {
        var exp = EntityQueryCompiler.Compile(@"people.where(age.isAny([99, 44]))", SchemaBuilder.FromObject<TestSchema>(), executionOptions, new DefaultMethodProvider());
        var data = new TestSchema();
        Assert.Equal(4, data.People.Count());
        var result = exp.Execute(data) as IEnumerable<Person>;
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Robin", result.ElementAt(0).Name);
    }

    // This would be your Entity/Object graph you use with EntityFramework
    private class TestSchema
    {
        public IEnumerable<Person> People =>
            [
                new Person
                {
                    Id = 9,
                    Name = "Bob",
                    Guid = Guid.NewGuid(),
                },
                new Person(),
                new Person
                {
                    Id = 9,
                    Name = "Boba",
                    Guid = Guid.NewGuid(),
                },
                new Person
                {
                    Id = 9,
                    Name = "Robin",
                    Guid = Guid.NewGuid(),
                    Age = 44,
                },
            ];
    }

    private class Person
    {
        public int Id { get; set; } = 99;
        public string Name { get; set; } = "Luke";
        public string LastName { get; set; } = "Lasty";
        public Guid Guid { get; set; } = new Guid("6492f5fe-0869-4279-88df-7f82f8e87a67");
        public int? Age { get; set; }
    }
}
