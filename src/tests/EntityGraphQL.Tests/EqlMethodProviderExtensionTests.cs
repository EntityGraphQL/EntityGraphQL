using System;
using EntityGraphQL.Compiler.EntityQuery;
using Xunit;

namespace EntityGraphQL.Tests;

public class EqlMethodProviderExtensionTests
{
    [Fact]
    public void Can_Extend_IsAny_SupportedTypes()
    {
        var provider = new EqlMethodProvider();
        Assert.False(provider.EntityTypeHasMethod(typeof(Version), "isAny"));

        provider.ExtendIsAnySupportedTypes(typeof(Version));
        Assert.True(provider.EntityTypeHasMethod(typeof(Version), "isAny"));
    }
}
