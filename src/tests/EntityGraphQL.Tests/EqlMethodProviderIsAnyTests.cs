using System;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class EqlMethodProviderIsAnyTests
{
    [Fact]
    public void IsAny_Default_Supports_Common_Scalars()
    {
        var provider = new EqlMethodProvider();
        // primitives
        Assert.True(provider.EntityTypeHasMethod(typeof(string), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(int), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(int?), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(double), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(decimal), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(uint), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(long), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(ulong), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(byte), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(char), "isAny"));
        // temporals & ids
        Assert.True(provider.EntityTypeHasMethod(typeof(DateTime), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(Guid), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(DateTimeOffset), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(TimeSpan), "isAny"));
#if NET8_0_OR_GREATER
        Assert.True(provider.EntityTypeHasMethod(typeof(DateOnly), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(TimeOnly), "isAny"));
#endif
    }

    [Theory]
    [InlineData(typeof(Version))]
    [InlineData(typeof(Uri))]
    public void IsAny_Can_Be_Extended_By_Type(Type type)
    {
        var provider = new EqlMethodProvider();
        Assert.False(provider.EntityTypeHasMethod(type, "isAny"));
        provider.ExtendIsAnySupportedTypes(type);
        Assert.True(provider.EntityTypeHasMethod(type, "isAny"));
    }

    [Fact]
    public void IsAny_Cant_Be_Extended_By_Type_Via_AddCustomTypeConverter_FromType()
    {
        var schema = new SchemaProvider<object>();
        Assert.False(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));
        schema.AddCustomTypeConverter<string>((v, t, sp) => t == typeof(Version) ? Version.Parse(v) : v);
        Assert.False(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));
    }

    [Fact]
    public void IsAny_Can_Be_Extended_By_SupportedToTypes_Via_AddCustomTypeConverter_FromType()
    {
        var schema = new SchemaProvider<object>();
        Assert.False(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));
        schema.AddCustomTypeConverter<string>((v, t, sp) => t == typeof(Version) ? Version.Parse(v) : v, typeof(Version));
        Assert.True(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));
    }

    [Fact]
    public void IsAny_Can_Be_Extended_By_Type_Via_AddCustomTypeConverter_ToType()
    {
        var schema = new SchemaProvider<object>();
        Assert.False(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));
        schema.AddCustomTypeConverter<Version>((o, sp) => Version.Parse(o!.ToString()!));
        Assert.True(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));
    }

    [Fact]
    public void IsAny_Can_Be_Extended_By_Type_Via_AddCustomTypeConverter_FromToType()
    {
        var schema = new SchemaProvider<object>();
        Assert.False(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));
        schema.AddCustomTypeConverter<string, Version>((o, sp) => Version.Parse(o.ToString()!));
        Assert.True(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));
    }

    private enum MyEnum
    {
        A = 1,
        B = 2,
    }

    [Fact]
    public void IsAny_When_Extended_With_ValueTypeTarget_Adds_Nullable_Variant()
    {
        var schema = new SchemaProvider<object>();
        // Precondition: enum and enum? are not supported by default
        Assert.False(schema.MethodProvider.EntityTypeHasMethod(typeof(MyEnum), "isAny"));
        Assert.False(schema.MethodProvider.EntityTypeHasMethod(typeof(MyEnum?), "isAny"));

        // Register converter targeting the enum type
        schema.AddCustomTypeConverter<string, MyEnum>((s, _) => (MyEnum)Enum.Parse(typeof(MyEnum), s, ignoreCase: true));

        // Both the enum and its nullable form should be supported now
        Assert.True(schema.MethodProvider.EntityTypeHasMethod(typeof(MyEnum), "isAny"));
        Assert.True(schema.MethodProvider.EntityTypeHasMethod(typeof(MyEnum?), "isAny"));
    }
}
