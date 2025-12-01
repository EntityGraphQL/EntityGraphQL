using System;
using System.Collections.Generic;
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
        schema.AddCustomTypeConverter<Uri>(
            (obj, _) =>
            {
                return obj switch
                {
                    string s => new Uri(s, UriKind.RelativeOrAbsolute),
                    Uri u => u,
                    _ => new Uri(obj!.ToString()!, UriKind.RelativeOrAbsolute),
                };
            }
        );

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
        schema.AddCustomTypeConverter<string>(
            (s, to, _) =>
            {
                if (to == typeof(Uri))
                    return new Uri(s, UriKind.RelativeOrAbsolute);
                if (to == typeof(Version))
                    return Version.Parse(s);
                return s; // no-op for other targets
            }
        );

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
        schema.AddCustomTypeConverter<string>(
            (string s, Type to, ISchemaProvider _, out object? result) =>
            {
                if (to == typeof(int) && int.TryParse(s, out var i))
                {
                    result = i;
                    return true;
                }
                else if (to == typeof(Guid) && Guid.TryParse(s, out var g))
                {
                    result = g;
                    return true;
                }
                result = null;
                return false;
            }
        );

        var guidStr = Guid.NewGuid().ToString();
        var r1 = ExpressionUtil.ConvertObjectType(guidStr, typeof(Guid), schema);
        Assert.IsType<Guid>(r1);

        var r2 = ExpressionUtil.ConvertObjectType("123", typeof(int), schema);
        Assert.Equal(123, r2);
    }

    [Fact]
    public void Converters_Array_To_List_And_Vice_Versa_Are_Converted()
    {
        var schema = new SchemaProvider<object>();
        // Register conversions for string -> Version
        schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));
        // Also support to-only for Version passthrough to help nested conversion
        schema.AddCustomTypeConverter<Version>((obj, _) => obj is Version v ? v : Version.Parse(obj!.ToString()!));

        // string[] -> List<Version>
        var arr = new[] { "1.2.3", "2.0.0" };
        var listResult = ExpressionUtil.ConvertObjectType(arr, typeof(List<Version>), schema);
        Assert.NotNull(listResult);
        var versionList = Assert.IsType<List<Version>>(listResult);
        Assert.Equal(new Version(1, 2, 3), versionList[0]);
        Assert.Equal(new Version(2, 0, 0), versionList[1]);

        // List<string> -> Version[]
        var strList = new List<string> { "3.0.0", "3.1.0" };
        var arrayResult = ExpressionUtil.ConvertObjectType(strList, typeof(Version[]), schema);
        Assert.NotNull(arrayResult);
        var versionArray = Assert.IsType<Version[]>(arrayResult);
        Assert.Equal(new Version(3, 0, 0), versionArray[0]);
        Assert.Equal(new Version(3, 1, 0), versionArray[1]);
    }

    [Fact]
    public void Converters_String_To_NullableInt_And_Null_Input_Remains_Null()
    {
        var schema = new SchemaProvider<object>();
        schema.AddCustomTypeConverter<string>(
            (string s, Type to, ISchemaProvider _, out object? result) =>
            {
                if ((to == typeof(int) || to == typeof(int?)) && int.TryParse(s, out var i))
                {
                    result = i;
                    return true;
                }
                result = null;
                return false;
            }
        );

        var r1 = ExpressionUtil.ConvertObjectType("123", typeof(int?), schema);
        Assert.NotNull(r1);
        Assert.Equal(123, Assert.IsType<int>(r1));

        object? input = null;
        var r2 = ExpressionUtil.ConvertObjectType(input, typeof(int?), schema);
        Assert.Null(r2);
    }

    private enum MyColor
    {
        Red = 1,
        Green = 2,
        Blue = 3,
    }

    [Fact]
    public void Converters_String_To_Enum_Succeeds_And_Invalid_Fails()
    {
        var schema = new SchemaProvider<object>();
        schema.AddCustomTypeConverter<string, MyColor>(
            (s, _) =>
            {
                if (Enum.TryParse<MyColor>(s, ignoreCase: true, out var val))
                    return val;
                throw new ArgumentException($"Invalid enum value '{s}' for {typeof(MyColor).Name}");
            }
        );

        var ok = ExpressionUtil.ConvertObjectType("Green", typeof(MyColor), schema);
        Assert.Equal(MyColor.Green, Assert.IsType<MyColor>(ok));

        var ex = Assert.ThrowsAny<Exception>(() => ExpressionUtil.ConvertObjectType("NotAColor", typeof(MyColor), schema));
        Assert.Contains("Invalid enum value", ex.Message);
    }
}
