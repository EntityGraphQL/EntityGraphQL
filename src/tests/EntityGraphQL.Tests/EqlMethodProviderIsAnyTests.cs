using System;
using EntityGraphQL.Compiler.EntityQuery;
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
#if NET6_0_OR_GREATER
        Assert.True(provider.EntityTypeHasMethod(typeof(DateOnly), "isAny"));
        Assert.True(provider.EntityTypeHasMethod(typeof(TimeOnly), "isAny"));
#endif
    }

    [Fact]
    public void IsAny_Can_Be_Extended_By_Type()
    {
        var provider = new EqlMethodProvider();
        Assert.False(provider.EntityTypeHasMethod(typeof(Version), "isAny"));
        provider.ExtendIsAnySupportedTypes(typeof(Version));
        Assert.True(provider.EntityTypeHasMethod(typeof(Version), "isAny"));
    }

    [Fact]
    public void IsAny_Can_Be_Extended_By_Predicate()
    {
        var provider = new EqlMethodProvider();
        Assert.False(provider.EntityTypeHasMethod(typeof(Uri), "isAny"));
        provider.ExtendIsAnyTypePredicate(t => t == typeof(Uri));
        Assert.True(provider.EntityTypeHasMethod(typeof(Uri), "isAny"));
    }
}
