using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class CustomConvertersWithBinaryAndIsAnyTests
{
    private readonly EqlCompileContext compileContext = new(new CompileContext(new ExecutionOptions(), null, new QueryRequestContext(null, null), null, null));

    private class WithVersion
    {
        public WithVersion(Version v, string name)
        {
            V = v;
            Name = name;
        }

        public Version V { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void Binary_Version_Uses_CustomConverter_For_String_Literals()
    {
        var schema = SchemaBuilder.FromObject<WithVersion>();
        schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));

        var compiled = EntityQueryCompiler.Compile("v >= \"1.2.3\"", schema, compileContext);
        var data = new List<WithVersion> { new(new Version(1, 2, 2), "A"), new(new Version(1, 2, 3), "B"), new(new Version(2, 0, 0), "C") };

        var res = data.Where((Func<WithVersion, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Equal(2, res.Count);
        Assert.Equal("B", res[0].Name);
        Assert.Equal("C", res[1].Name);
    }

    [Fact]
    public void IsAny_On_String_And_Binary_On_Version_With_CustomConverter()
    {
        var schema = SchemaBuilder.FromObject<WithVersion>();
        schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));

        var compiled = EntityQueryCompiler.Compile("name.isAny([\"B\", \"C\"]) && v >= \"1.2.3\"", schema, compileContext, schema.MethodProvider);
        var data = new List<WithVersion> { new(new Version(1, 2, 2), "A"), new(new Version(1, 2, 3), "B"), new(new Version(2, 0, 0), "C"), new(new Version(3, 0, 0), "D") };

        var res = data.Where((Func<WithVersion, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Equal(2, res.Count);
        Assert.Equal("B", res[0].Name);
        Assert.Equal("C", res[1].Name);
    }

    [Fact]
    public void IsAny_On_String_And_Binary_On_Version_With_ToOnly_Converter()
    {
        var schema = SchemaBuilder.FromObject<WithVersion>();
        // String isAny supported by default; add custom type converter for Version and also a to-only converter
        schema.AddCustomTypeConverter<Version>(
            (obj, _) =>
            {
                return obj switch
                {
                    Version v => v,
                    string s => Version.Parse(s),
                    _ => Version.Parse(obj!.ToString()!),
                };
            }
        );

        var compiled = EntityQueryCompiler.Compile("name.isAny([\"Hit\"]) && v >= \"1.2.3\" ", schema, compileContext, schema.MethodProvider);
        var data = new List<WithVersion> { new(new Version(1, 2, 3), "Hit"), new(new Version(1, 2, 4), "Miss") };

        var res = data.Where((Func<WithVersion, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Single(res);
        Assert.Equal("Hit", res[0].Name);
    }

    [Fact]
    public void Binary_Version_With_Variable_Converted_By_FromTo_Converter()
    {
        var schema = SchemaBuilder.FromObject<WithVersion>();
        // Register a precise from-to converter for string -> Version (used for variables/JSON path)
        schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));

        // Build lambda that compares V >= $min using ExpressionUtil on the variable
        var param = Expression.Parameter(typeof(WithVersion), "w");
        var left = Expression.Property(param, nameof(WithVersion.V));
        // simulate variable coming as string and converted by custom converters
        object? variable = "1.2.3";
        var converted = ExpressionUtil.ConvertObjectType(variable, typeof(Version), schema);
        var right = Expression.Constant((Version)converted!, typeof(Version));
        var body = Expression.GreaterThanOrEqual(left, right);
        var lambda = Expression.Lambda<Func<WithVersion, bool>>(body, param).Compile();

        var data = new List<WithVersion> { new(new Version(1, 2, 2), "A"), new(new Version(1, 2, 3), "B"), new(new Version(2, 0, 0), "C") };
        var res = data.Where(lambda).ToList();
        Assert.Equal(2, res.Count);
        Assert.Equal("B", res[0].Name);
        Assert.Equal("C", res[1].Name);
    }

    [Fact]
    public void Null_Variable_Is_Ignored_By_Custom_Converters_And_Returns_Null()
    {
        var schema = SchemaBuilder.FromObject<WithVersion>();
        schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));

        object? variable = null;
        var converted = ExpressionUtil.ConvertObjectType(variable, typeof(Version), schema);
        Assert.Null(converted); // Null bypasses custom converters and remains null
    }

    [Fact]
    public void Combining_Extensions_In_Single_Query_And_Execution_Path()
    {
        // Arrange
        var schema = SchemaBuilder.FromObject<WithVersion>();
        // Register custom converters for Version (from-to and to-only). This automatically enables isAny for Version
        schema.AddCustomTypeConverter<Version>((obj, _) => obj is Version v ? v : Version.Parse(obj!.ToString()!));
        Assert.True(schema.MethodProvider.EntityTypeHasMethod(typeof(Version), "isAny"));

        // Query combines: string isAny + binary (uses literal parser)
        var compiled = EntityQueryCompiler.Compile("name.isAny([\"B\", \"C\"]) && v >= \"1.2.3\"", schema, compileContext, schema.MethodProvider);

        var data = new List<WithVersion> { new(new Version(1, 2, 2), "A"), new(new Version(1, 2, 3), "B"), new(new Version(2, 0, 0), "C"), new(new Version(3, 0, 0), "D") };

        var res = data.Where((Func<WithVersion, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Equal(new[] { "B", "C" }, res.Select(r => r.Name).ToArray());

        // Additionally, validate custom converters on a Version list used similarly to isAny semantics
        var versionSources = new[] { "1.2.3", "2.0.0" };
        var convertedList = (IEnumerable<object?>)ExpressionUtil.ConvertObjectType(versionSources, typeof(List<Version>), schema)!;
        var versionSet = convertedList.Cast<Version>().ToHashSet();
        var resContains = data.Where(d => versionSet.Contains(d.V)).Select(d => d.Name).ToList();
        Assert.Equal(new[] { "B", "C" }, resContains);
    }
}
