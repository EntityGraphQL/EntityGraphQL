using System;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class CustomTypeConvertersGenericTestsCombinations
{
    private sealed class LegacyStringConverter : ICustomTypeConverter
    {
        private readonly Action _onCalled;
        private readonly Func<string, Type, object?> _convert;
        public LegacyStringConverter(Action onCalled, Func<string, Type, object?> convert)
        {
            _onCalled = onCalled;
            _convert = convert;
        }
        public Type Type => typeof(string);
        public object? ChangeType(object value, Type toType, ISchemaProvider schema)
        {
            _onCalled();
            var s = (string)value;
            return _convert(s, toType);
        }
    }

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
        schema.AddCustomTypeConverter<string, Uri>((s, _) => new Uri("fromto:" + s, UriKind.RelativeOrAbsolute));

        var input = "abc";
        var output = ExpressionUtil.ConvertObjectType(input, typeof(Uri), schema);

        Assert.IsType<Uri>(output);
        Assert.Equal(new Uri("fromto:abc", UriKind.RelativeOrAbsolute), (Uri)output!);
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
        Assert.Equal(new Uri("toonly:zzz", UriKind.RelativeOrAbsolute), (Uri)output!);
    }

    [Fact]
    public void Precedence_FromOnly_Wins_Over_Legacy()
    {
        var schema = new SchemaProvider<object>();
        var legacyCalled = false;
        var fromOnlyCalled = false;

        // legacy string converter (should not be hit)
        schema.AddCustomTypeConverter(new LegacyStringConverter(() => legacyCalled = true, (s, to) =>
        {
            if (to == typeof(Version))
                return Version.Parse("9.9.9.9");
            return s; // passthrough
        }));

        // from-only string converter (should win)
        schema.AddCustomTypeConverter<string>((string s, Type to, ISchemaProvider _, out object? result) =>
        {
            fromOnlyCalled = true;
            if (to == typeof(Version))
            {
                result = Version.Parse("1.2.3.4");
                return true;
            }
            result = null;
            return false;
        });

        var output = ExpressionUtil.ConvertObjectType("1.0.0.0", typeof(Version), schema);
        Assert.IsType<Version>(output);
        Assert.Equal(Version.Parse("1.2.3.4"), (Version)output!);
        Assert.True(fromOnlyCalled);
        Assert.False(legacyCalled);
    }

    [Fact]
    public void Precedence_AllFour_FromTo_ToOnly_FromOnly_Legacy()
    {
        var schema = new SchemaProvider<object>();
        var legacyCalled = false;
        var fromOnlyCalled = false;
        var toOnlyCalled = false;
        var fromToCalled = false;

        // legacy string (last)
        schema.AddCustomTypeConverter(new LegacyStringConverter(() => legacyCalled = true, (s, to) => s));

        // from-only string
        schema.AddCustomTypeConverter<string>((string s, Type to, ISchemaProvider _, out object? result) =>
        {
            fromOnlyCalled = true;
            if (to == typeof(int)) { result = 100; return true; }
            result = null; return false;
        });

        // to-only int
        TypeConverterTryTo<int> toOnlyInt = (object? obj, Type to, ISchemaProvider _, out int result) =>
        {
            toOnlyCalled = true;
            result = 200; return true;
        };
        schema.AddCustomTypeConverter<int>(toOnlyInt);

        // from-to string->int (should win)
        schema.AddCustomTypeConverter<string, int>((s, _) => { fromToCalled = true; return 300; });

        var output = ExpressionUtil.ConvertObjectType("x", typeof(int), schema);
        Assert.IsType<int>(output);
        Assert.Equal(300, (int)output!);
        Assert.True(fromToCalled);
        Assert.False(toOnlyCalled);
        Assert.False(fromOnlyCalled);
        Assert.False(legacyCalled);
    }

    [Fact]
    public void Null_Input_With_Multiple_Converters_Returns_Null_And_Does_Not_Invoke_Converters()
    {
        var schema = new SchemaProvider<object>();
        var legacyCalled = false;
        var fromOnlyCalled = false;
        var toOnlyCalled = false;
        var fromToCalled = false;

        // Register all kinds
        schema.AddCustomTypeConverter(new LegacyStringConverter(() => legacyCalled = true, (s, to) => s));
        schema.AddCustomTypeConverter<string, int>((s, _) => { fromToCalled = true; return 1; });
        TypeConverterTryTo<int> toOnlyInt2 = (object? obj, Type to, ISchemaProvider _, out int result) => { toOnlyCalled = true; result = 2; return true; }; schema.AddCustomTypeConverter<int>(toOnlyInt2);
        schema.AddCustomTypeConverter<string>((string s, Type to, ISchemaProvider _, out object? result) => { fromOnlyCalled = true; result = 3; return true; });

        object? input = null;
        var output = ExpressionUtil.ConvertObjectType(input, typeof(int), schema);
        Assert.Null(output);
        Assert.False(fromToCalled);
        Assert.False(toOnlyCalled);
        Assert.False(fromOnlyCalled);
        Assert.False(legacyCalled);
    }

    [Fact]
    public void Legacy_Converter_Is_Invoked_When_No_Generic_Converters_Apply()
    {
        var schema = new SchemaProvider<object>();
        var legacyCalled = false;

        // Only legacy converter registered; convert string->Uri
        schema.AddCustomTypeConverter(new LegacyStringConverter(() => legacyCalled = true, (s, to) =>
        {
            if (to == typeof(Uri))
                return new Uri("legacy:" + s, UriKind.RelativeOrAbsolute);
            return s;
        }));

        var output = ExpressionUtil.ConvertObjectType("abc", typeof(Uri), schema);
        Assert.IsType<Uri>(output);
        Assert.Equal(new Uri("legacy:abc", UriKind.RelativeOrAbsolute), (Uri)output!);
        Assert.True(legacyCalled);
    }
}
