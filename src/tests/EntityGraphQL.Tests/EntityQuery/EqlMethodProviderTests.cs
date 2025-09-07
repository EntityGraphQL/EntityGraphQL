using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests.EntityQuery;

public class EqlMethodProviderTests
{
    private readonly CompileContext compileContext = new(new ExecutionOptions(), null, new QueryRequestContext(null, null), null, null);

    [Fact]
    public void TestMethodRegistration()
    {
        var provider = new EqlMethodProvider();

        var regexMethod = typeof(SharedTestExtensions).GetMethod(nameof(SharedTestExtensions.Regex));
        Assert.NotNull(regexMethod);

        provider.RegisterMethod(regexMethod!, typeof(string), "regex");

        // Should have registered the direct method
        var registeredMethods = provider.GetCustomRegisteredMethods();
        Assert.Single(registeredMethods);

        var regexMethodInfo = registeredMethods.First();
        Assert.Equal("regex", regexMethodInfo.MethodName);
        Assert.Equal(typeof(string), regexMethodInfo.MethodContextType);
    }

    [Fact]
    public void TestNamingConflicts()
    {
        var provider = new EqlMethodProvider();

        var regexMethod = typeof(SharedTestExtensions).GetMethod(nameof(SharedTestExtensions.Regex));
        Assert.NotNull(regexMethod);

        // First registration should succeed
        provider.RegisterMethod(regexMethod!, typeof(string), "regex");

        // Second registration with same name should throw
        Assert.Throws<InvalidOperationException>(() => provider.RegisterMethod(regexMethod!, typeof(string), "regex"));
    }

    [Fact]
    public void TestClearAllMethods()
    {
        var provider = new EqlMethodProvider();

        // Register both extension and direct methods
        provider.RegisterMethods(typeof(SharedTestExtensions));

        // Should have registered methods
        Assert.True(provider.GetRegisteredMethods().Count > 0);

        provider.ClearAllMethods();

        // Should be empty after clearing
        Assert.Empty(provider.GetRegisteredMethods());
    }

    [Fact]
    public void TestTypeCompatibility()
    {
        var provider = new EqlMethodProvider();

        var regexMethod = typeof(SharedTestExtensions).GetMethod(nameof(SharedTestExtensions.Regex));
        var matchesMethod = typeof(SharedTestExtensions).GetMethod(nameof(SharedTestExtensions.MatchesLength));

        Assert.NotNull(regexMethod);
        Assert.NotNull(matchesMethod);

        provider.RegisterMethod(regexMethod!, typeof(string), "regex");
        provider.RegisterMethod(matchesMethod!, typeof(double), "matchesLength");

        // Should only work on correct types
        Assert.True(provider.EntityTypeHasMethod(typeof(string), "regex"));
        Assert.False(provider.EntityTypeHasMethod(typeof(int), "regex"));

        Assert.True(provider.EntityTypeHasMethod(typeof(double), "matchesLength"));
        Assert.False(provider.EntityTypeHasMethod(typeof(string), "matchesLength"));
    }

    [Fact]
    public void TestAutoNamingWithCamelCase()
    {
        var provider = new EqlMethodProvider();

        var regexMethod = typeof(SharedTestExtensions).GetMethod(nameof(SharedTestExtensions.Regex));
        Assert.NotNull(regexMethod);

        // Register without specifying name - should auto-generate camelCase name
        provider.RegisterMethod(regexMethod!, typeof(string));

        var registeredMethods = provider.GetCustomRegisteredMethods();
        Assert.Single(registeredMethods);

        var methodInfo = registeredMethods.First();
        Assert.Equal("regex", methodInfo.MethodName);
    }

    [Fact]
    public void EqlMethodProvider_Should_Allow_Custom_Method_Registration()
    {
        // Arrange
        var provider = new EqlMethodProvider();

        // Register a custom method for testing
        provider.RegisterMethod(typeof(string), "customTest", (context, argContext, methodName, args) => Expression.Constant(true));

        // Act & Assert
        Assert.True(provider.EntityTypeHasMethod(typeof(string), "customTest"));
        Assert.False(provider.EntityTypeHasMethod(typeof(int), "customTest"));
    }

    [Fact]
    public void EqlMethodProvider_Should_Prevent_Method_Name_Conflicts()
    {
        // Arrange
        var provider = new EqlMethodProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => provider.RegisterMethod(typeof(string), "contains", (context, argContext, methodName, args) => Expression.Constant(true)));

        Assert.Contains("already registered", exception.Message);
    }

    [Fact]
    public void EqlMethodProvider_Should_Allow_Clearing_Custom_Methods_Only()
    {
        // Arrange
        var provider = new EqlMethodProvider();

        // Register a custom method
        provider.RegisterMethod(typeof(string), "customMethod", (context, argContext, methodName, args) => Expression.Constant(true));

        Assert.True(provider.EntityTypeHasMethod(typeof(string), "customMethod"));
        Assert.True(provider.EntityTypeHasMethod(typeof(string), "contains")); // Default method

        // Act
        provider.ClearCustomMethods();

        // Assert
        Assert.False(provider.EntityTypeHasMethod(typeof(string), "customMethod"));
        Assert.True(provider.EntityTypeHasMethod(typeof(string), "contains")); // Default method should remain
    }

    [Fact]
    public void EqlMethodProvider_Test_AddingStaticMethod()
    {
        var provider = new EqlMethodProvider();
        // Register an instance method
        provider.RegisterMethod(typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!, typeof(string));
        Assert.True(provider.EntityTypeHasMethod(typeof(string), "compare"));

        var exp = EntityQueryCompiler.Compile(@"people.first(name.compare(""Bob"") == 0)", SchemaBuilder.FromObject<EqlMethodTestSchema>(), compileContext, provider);
        var result = exp.Execute(new EqlMethodTestSchema()) as Person;
        Assert.NotNull(result);
        Assert.Equal("Bob", result.Name);
    }

    [Fact]
    public void EqlMethodProvider_Test_AddingInstanceMethod()
    {
        var provider = new EqlMethodProvider();
        // Register an instance method
        provider.RegisterMethod(typeof(Person).GetMethod(nameof(Person.GetFullName))!);
        Assert.True(provider.EntityTypeHasMethod(typeof(Person), "getFullName"));

        var exp = EntityQueryCompiler.Compile(@"people.first(getFullName() == ""Robin Hood"")", SchemaBuilder.FromObject<EqlMethodTestSchema>(), compileContext, provider);
        var result = exp.Execute(new EqlMethodTestSchema()) as Person;
        Assert.NotNull(result);
        Assert.Equal("Robin Hood", result.GetFullName());
    }

    [Fact]
    public void EqlMethodProvider_Test_AddingCustomMakeCallFunc()
    {
        var provider = new EqlMethodProvider();
        provider.RegisterMethod(
            t => t == typeof(int) || t == typeof(string),
            "isOne",
            (context, argContext, methodName, args) =>
            {
                if (context.Type == typeof(int))
                {
                    return Expression.MakeBinary(ExpressionType.Equal, context, Expression.Constant(1));
                }
                return Expression.MakeBinary(ExpressionType.Equal, context, Expression.Constant("1"));
            }
        );

        var exp = EntityQueryCompiler.Compile(@"one.isOne()", SchemaBuilder.FromObject<EqlMethodTestSchema>(), compileContext, provider);
        Assert.True(exp.Execute(new EqlMethodTestSchema()) as bool?);
        exp = EntityQueryCompiler.Compile(@"notOne.isOne()", SchemaBuilder.FromObject<EqlMethodTestSchema>(), compileContext, provider);
        Assert.False(exp.Execute(new EqlMethodTestSchema()) as bool?);

        exp = EntityQueryCompiler.Compile(@"oneStr.isOne()", SchemaBuilder.FromObject<EqlMethodTestSchema>(), compileContext, provider);
        Assert.True(exp.Execute(new EqlMethodTestSchema()) as bool?);
        exp = EntityQueryCompiler.Compile(@"notOneStr.isOne()", SchemaBuilder.FromObject<EqlMethodTestSchema>(), compileContext, provider);
        Assert.False(exp.Execute(new EqlMethodTestSchema()) as bool?);
    }

    private class EqlMethodTestSchema
    {
        public int One => 1;
        public int NotOne => 2;
        public string OneStr => "1";
        public string NotOneStr => "11";
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
                    LastName = "Hood",
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

        public string? GetFullName() => $"{Name} {LastName}";
    }
}

internal static class SharedTestExtensions
{
    [EqlMethod("regex")]
    public static bool Regex(this string input, string pattern)
    {
        if (input == null)
            return false;
        return System.Text.RegularExpressions.Regex.IsMatch(input, pattern);
    }

    [EqlMethod("matchesLength")]
    public static bool MatchesLength(this string input, int length)
    {
        return input?.Length == length;
    }
}
