using System;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class CustomTypeConvertersGenericTestsCombinations
{
    [Fact]
    public void Precedence_FromTo_Wins_Over_ToOnly()
    {
        var schema = new SchemaProvider<object>();
        var toOnlyCalled = false;

        // to-only converter for Uri (should not be hit)
        TypeConverterTryTo<Uri> toOnlyUri = (object? obj, Type to, ISchemaProvider s, out Uri result) =>
        {
            toOnlyCalled = true;
            result = new Uri("toonly:" + (obj?.ToString() ?? "null"), UriKind.RelativeOrAbsolute);
            return true;
        };
        schema.AddCustomTypeConverter<Uri>(toOnlyUri);

        // from-to converter for string->Uri (should win)
        schema.AddCustomTypeConverter<string, Uri>((s, _) =>
            new Uri("fromto:" + s, UriKind.RelativeOrAbsolute));

        const string input = "abc";
        var output = ExpressionUtil.ConvertObjectType(input, typeof(Uri), schema);

        Assert.IsType<Uri>(output);
        Assert.Equal(new Uri("fromto:abc", UriKind.RelativeOrAbsolute), (Uri)output);
        Assert.False(toOnlyCalled);
    }

    [Fact]
    public void Precedence_ToOnly_Wins_Over_FromOnly()
    {
        var schema = new SchemaProvider<object>();
        var toOnlyCalled = false;
        var fromOnlyCalled = false;

        // from-only for string (should not be hit if to-only matches)
        schema.AddCustomTypeConverter<string>((string s, Type to, ISchemaProvider _, out object? result) =>
        {
            fromOnlyCalled = true;
            if (to == typeof(Uri))
            {
                result = new Uri("fromonly:" + s, UriKind.RelativeOrAbsolute);
                return true;
            }
            result = null;
            return false;
        });

        // to-only for Uri (should win)
        schema.AddCustomTypeConverter<Uri>((object? obj, Type to, ISchemaProvider _, out Uri result) =>
        {
            toOnlyCalled = true;
            result = obj as Uri ?? new Uri("toonly:" + (obj?.ToString() ?? "null"), UriKind.RelativeOrAbsolute);
            return true;
        });

        var output = ExpressionUtil.ConvertObjectType("zzz", typeof(Uri), schema);
        Assert.IsType<Uri>(output);
        Assert.True(toOnlyCalled);
        Assert.False(fromOnlyCalled);
        Assert.Equal(new Uri("toonly:zzz", UriKind.RelativeOrAbsolute), (Uri)output);
    }

    [Fact]
    public void Null_Input_With_Multiple_Converters_Returns_Null_And_Does_Not_Invoke_Converters()
    {
        var schema = new SchemaProvider<object>();
        var fromOnlyCalled = false;
        var toOnlyCalled = false;
        var fromToCalled = false;

        // Register various kinds
        schema.AddCustomTypeConverter<string, int>((s, _) =>
        {
            fromToCalled = true; 
            return 1;
        });
        
        TypeConverterTryTo<int> toOnlyInt = (object? obj, Type to, ISchemaProvider _, out int result) =>
        {
            toOnlyCalled = true; result = 2; 
            return true;
        };
        schema.AddCustomTypeConverter<int>(toOnlyInt);
        
        schema.AddCustomTypeConverter<string>((string s, Type to, ISchemaProvider _, out object? result) =>
        {
            fromOnlyCalled = true; 
            result = 3; 
            return true;
        });

        object? input = null;
        var output = ExpressionUtil.ConvertObjectType(input, typeof(int), schema);
        Assert.Null(output);
        Assert.False(fromToCalled);
        Assert.False(toOnlyCalled);
        Assert.False(fromOnlyCalled);
    }
}
