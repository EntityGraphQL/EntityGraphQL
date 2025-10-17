using System;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class CustomTypeConvertersGenericTests
{
    [Fact]
    public void FromToConverter_Func_StringToUri()
    {
        var schema = new SchemaProvider<object>();
        schema.AddCustomTypeConverter<string, Uri>((s, _) => new Uri(s, UriKind.RelativeOrAbsolute));

        var input = "https://example.com/a";
        var result = ExpressionUtil.ConvertObjectType(input, typeof(Uri), schema);
        Assert.NotNull(result);
        Assert.IsType<Uri>(result);
        Assert.Equal(new Uri(input), (Uri)result!);
    }

    [Fact]
    public void FromToConverter_Try_StringToGuid()
    {
        var schema = new SchemaProvider<object>();
        schema.AddCustomTypeConverter<string, Guid>((string s, ISchemaProvider _, out Guid g) => Guid.TryParse(s, out g));

        var input = Guid.NewGuid().ToString();
        var result = ExpressionUtil.ConvertObjectType(input, typeof(Guid), schema);
        Assert.NotNull(result);
        Assert.IsType<Guid>(result);
        Assert.Equal(Guid.Parse(input), (Guid)result!);
    }

    [Fact]
    public void ToTypeConverter_Func_ObjectToUri()
    {
        var schema = new SchemaProvider<object>();
        schema.AddCustomTypeConverter<Uri>((obj, _) =>
        {
            return obj switch
            {
                string s => new Uri(s, UriKind.RelativeOrAbsolute),
                Uri u => u,
                _ => new Uri(obj!.ToString()!, UriKind.RelativeOrAbsolute),
            };
        });

        var input = "https://example.com/b";
        var result = ExpressionUtil.ConvertObjectType(input, typeof(Uri), schema);
        Assert.NotNull(result);
        Assert.IsType<Uri>(result);
        Assert.Equal(new Uri(input), (Uri)result!);
    }
    
    [Fact]
    public void FromConverter_Func_StringSources()
    {
        var schema = new SchemaProvider<object>();
        // from-only: string => toType can be Uri or Version
        schema.AddCustomTypeConverter<string>((s, to, _) =>
        {
            if (to == typeof(Uri))
                return new Uri(s, UriKind.RelativeOrAbsolute);
            if (to == typeof(Version))
                return Version.Parse(s);
            return s; // no-op for other targets
        });

        var input1 = "https://example.com/c";
        var r1 = ExpressionUtil.ConvertObjectType(input1, typeof(Uri), schema);
        Assert.IsType<Uri>(r1);

        var input2 = "1.2.3.4";
        var r2 = ExpressionUtil.ConvertObjectType(input2, typeof(Version), schema);
        Assert.IsType<Version>(r2);
    }

    [Fact]
    public void FromConverter_Try_StringSources()
    {
        var schema = new SchemaProvider<object>();
        schema.AddCustomTypeConverter<string>((string s, Type to, ISchemaProvider _, out object? result) =>
        {
            if (to == typeof(int))
            {
                if (int.TryParse(s, out var i)) { result = i; return true; }
            }
            else if (to == typeof(Guid))
            {
                if (Guid.TryParse(s, out var g)) { result = g; return true; }
            }
            result = null;
            return false;
        });

        var guidStr = Guid.NewGuid().ToString();
        var r1 = ExpressionUtil.ConvertObjectType(guidStr, typeof(Guid), schema);
        Assert.IsType<Guid>(r1);

        var r2 = ExpressionUtil.ConvertObjectType("123", typeof(int), schema);
        Assert.Equal(123, r2);
    }
}
