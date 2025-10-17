using System;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class CustomTypeConvertersGenericTestsNullCases
{
    [Fact]
    public void NullValue_BypassesCustomConverters_AndReturnsNull()
    {
        var schema = new SchemaProvider<object>();
        bool fromToCalled = false;
        schema.AddCustomTypeConverter<string, Uri>((s, _) => { fromToCalled = true; return new Uri(s, UriKind.RelativeOrAbsolute); });

        object? input = null;
        var result = ExpressionUtil.ConvertObjectType(input, typeof(Uri), schema);
        Assert.Null(result);
        Assert.False(fromToCalled);
    }
}
