using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class LiteralParserExtensionTests
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
    public void Binary_Uses_Custom_Literal_Parser_For_Target_Type()
    {
        var schema = SchemaBuilder.FromObject<WithVersion>();
        // Register a literal parser for Version so string literals can be parsed at compile-time
        EntityQueryCompiler.RegisterLiteralParser<Version>(strExpr => Expression.Call(typeof(Version), nameof(Version.Parse), null, strExpr));

        var compiled = EntityQueryCompiler.Compile("v >= \"1.2.3\"", schema, compileContext);
        var data = new List<WithVersion> { new(new Version(1, 2, 2), "A"), new(new Version(1, 2, 3), "B"), new(new Version(2, 0, 0), "C") };

        var res = data.Where((Func<WithVersion, bool>)compiled.LambdaExpression.Compile()).ToList();
        Assert.Equal(2, res.Count);
        Assert.Equal("B", res[0].Name);
        Assert.Equal("C", res[1].Name);
    }
}
