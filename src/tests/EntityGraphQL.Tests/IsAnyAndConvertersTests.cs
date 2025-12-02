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

public class IsAnyAndConvertersTests
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
        public string Name { get; set; } = string.Empty;
    }

    private class WithNullableName
    {
        public WithNullableName(string? name)
        {
            Name = name;
        }

        public string? Name { get; set; }
    }

    [Fact]
    public void IsAny_For_Version_With_String_List_Uses_Converters()
    {
        var schema = SchemaBuilder.FromObject<WithVersion>();
        schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));

        var compiled = EntityQueryCompiler.Compile("v.isAny([\"1.2.3\", \"2.0.0\"]) ", schema, compileContext, schema.MethodProvider);
        var data = new[] { new WithVersion(new Version(1, 2, 2), "A"), new WithVersion(new Version(1, 2, 3), "B"), new WithVersion(new Version(2, 0, 0), "C") };
        var res = data.Where((Func<WithVersion, bool>)compiled.LambdaExpression.Compile()).Select(d => d.Name).ToArray();
        Assert.Equal(new[] { "B", "C" }, res);
    }

    [Fact]
    public void IsAny_On_CustomType_Is_Auto_Extended_By_Converters()
    {
        var schema = SchemaBuilder.FromObject<WithVersion>();
        // Adding a converter to Version should automatically enable isAny on Version
        schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));
        var compiled = EntityQueryCompiler.Compile("v.isAny([\"1.2.3\"]) ", schema, compileContext, schema.MethodProvider);
        var data = new[] { new WithVersion(new Version(1, 2, 2), "A"), new WithVersion(new Version(1, 2, 3), "B") };
        var res = data.Where((Func<WithVersion, bool>)compiled.LambdaExpression.Compile()).Select(d => d.Name).ToArray();
        Assert.Equal(new[] { "B" }, res);
    }

    [Fact]
    public void IsAny_With_Nullable_Field_And_List_With_Null_Works()
    {
        var schema = SchemaBuilder.FromObject<WithNullableName>();
        var compiled = EntityQueryCompiler.Compile("name.isAny([null, \"A\"]) ", schema, compileContext, schema.MethodProvider);
        var data = new[] { new WithNullableName(null), new WithNullableName("A"), new WithNullableName("B") };
        var names = data.Where((Func<WithNullableName, bool>)compiled.LambdaExpression.Compile()).Select(d => d.Name).ToArray();
        Assert.Equal(new string?[] { null, "A" }, names);
    }

    [Fact]
    public void Query_Mixed_Binary_LiteralParser_And_IsAny_With_Variable_Strings_Simulated()
    {
        // This test simulates variable conversion path by pre-converting a string list via converters
        var schema = SchemaBuilder.FromObject<WithVersion>();
        EntityQueryCompiler.RegisterLiteralParser<Version>(strExpr => Expression.Call(typeof(Version), nameof(Version.Parse), null, strExpr));
        schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));

        var compiled = EntityQueryCompiler.Compile("v >= \"1.2.3\"", schema, compileContext);
        var data = new[]
        {
            new WithVersion(new Version(1, 2, 2), "A"),
            new WithVersion(new Version(1, 2, 3), "B"),
            new WithVersion(new Version(2, 0, 0), "C"),
            new WithVersion(new Version(3, 0, 0), "D"),
        };

        // simulate $versions variable provided as strings and converted
        var versionStrings = new[] { "1.2.3", "2.0.0" };
        var converted = (IEnumerable<object?>)ExpressionUtil.ConvertObjectType(versionStrings, typeof(List<Version>), schema)!;
        var versionSet = converted.Cast<Version>().ToHashSet();

        var pred = (Func<WithVersion, bool>)compiled.LambdaExpression.Compile();
        var res = data.Where(w => pred(w) && versionSet.Contains(w.V)).Select(w => w.Name).ToArray();
        Assert.Equal(new[] { "B", "C" }, res);
    }
}
