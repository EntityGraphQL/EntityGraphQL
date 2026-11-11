using System;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class ProcessArgumentValueTests
{
    [Fact]
    public void TestParseArgumentFloat()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1.2, typeof(float));
        Assert.Equal(1.2f, res);
    }

    [Fact]
    public void TestParseArgumentDouble()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1.2, typeof(double));
        Assert.Equal(1.2d, res);
    }

#if !NET11_0_OR_GREATER
    [Fact]
    public void NumberConversionMatchesDecimalConversionBeforeNet11()
    {
        // before .NET 11 the runtime's own conversion is the behaviour NumberConversion keeps on every target. They
        // agree for any value of up to 15 significant digits; for 16-17 digits the runtime's approximate rounding
        // can be one off in the 15th digit when the 16th is a 5
        var random = new System.Random(42);
        for (var i = 0; i < 100000; i++)
        {
            var digits = random.Next(1, 16);
            var mantissa = (long)(random.NextDouble() * System.Math.Pow(10, digits));
            var d = (random.Next(2) == 0 ? -1 : 1) * mantissa * System.Math.Pow(10, random.Next(-12 - digits, 13 - digits));
            Assert.Equal((decimal)d, EntityGraphQL.Compiler.Util.NumberConversion.ToDecimal(d));
            var f = (float)(mantissa % 10000000 * System.Math.Pow(10, random.Next(-6, 6)));
            Assert.Equal((decimal)f, EntityGraphQL.Compiler.Util.NumberConversion.ToDecimal(f));
        }
    }
#endif

    [Fact]
    public void TestParseArgumentDecimal()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1.2, typeof(decimal));
        Assert.Equal(1.2m, res);
    }

    [Theory]
    // a double is rounded to 15 significant digits for decimal, as .NET did before 11, on every target
    [InlineData(0.30000000000000004, "0.3")]
    [InlineData(123456789.123456789, "123456789.123457")]
    public void TestParseArgumentDecimalFromDoubleRoundsTo15Digits(double value, string expected)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), GraphQLParser.ConvertArgumentValue(schema, value, typeof(decimal)));
    }

    [Fact]
    public void TestParseArgumentFloatNoFraction()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1, typeof(float));
        Assert.Equal(1f, res);
    }

    [Fact]
    public void TestParseArgumentDoubleNoFraction()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1, typeof(double));
        Assert.Equal(1d, res);
    }

    [Fact]
    public void TestParseArgumentDecimalNoFraction()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1, typeof(decimal));
        Assert.Equal(1m, res);
    }

    [Fact]
    public void TestParseArgumentFloatNull()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1.2, typeof(float?));
        Assert.Equal(1.2f, res);
    }

    [Fact]
    public void TestParseArgumentDoubleNull()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1.2, typeof(double?));
        Assert.Equal(1.2d, res);
    }

    [Fact]
    public void TestParseArgumentDecimalNull()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 1.2, typeof(decimal?));
        Assert.Equal(1.2m, res);
    }

    [Fact]
    public void TestParseArgumentFloatNullValue()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, null, typeof(float?));
        Assert.Equal((float?)null, res);
    }

    [Fact]
    public void TestParseArgumentDoubleNullValue()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, null, typeof(double?));
        Assert.Equal((double?)null, res);
    }

    [Fact]
    public void TestParseArgumentDecimalNullValue()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, null, typeof(decimal?));
        Assert.Equal((decimal?)null, res);
    }

    [Fact]
    public void TestParseArgumentShort()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 123, typeof(short));
        Assert.Equal((short)123, res);
    }

    [Fact]
    public void TestParseArgumentUShort()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 123, typeof(ushort));
        Assert.Equal((ushort)123, res);
    }

    [Fact]
    public void TestParseArgumentUInt()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 123, typeof(uint));
        Assert.Equal(123u, res);
    }

    [Fact]
    public void TestParseArgumentLong()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 123, typeof(long));
        Assert.Equal(123L, res);
    }

    [Fact]
    public void TestParseArgumentULong()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var res = GraphQLParser.ConvertArgumentValue(schema, 123, typeof(ulong));
        Assert.Equal(123UL, res);
    }

    [Fact]
    public void TestMutationWithShortArgument()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<short>("Short", "A 16-bit signed integer");

        schema.Mutation().Add("addShort", (short value) => value + 1);

        var gql = new QueryRequest { Query = "mutation { addShort(value: 100) }" };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(101, (int)result.Data!["addShort"]!);
    }

    [Fact]
    public void TestMutationWithUShortArgument()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<ushort>("UShort", "A 16-bit unsigned integer");

        schema.Mutation().Add("addUShort", (ushort value) => value + 1);

        var gql = new QueryRequest { Query = "mutation { addUShort(value: 100) }" };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(101, (int)result.Data!["addUShort"]!);
    }

    [Fact]
    public void TestMutationWithUIntArgument()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<uint>("UInt", "A 32-bit unsigned integer");

        schema.Mutation().Add("addUInt", (uint value) => value + 1);

        var gql = new QueryRequest { Query = "mutation { addUInt(value: 100) }" };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(101, Convert.ToInt32(result.Data!["addUInt"]!));
    }

    [Fact]
    public void TestMutationWithLongArgument()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<long>("Long", "A 64-bit signed integer");

        schema.Mutation().Add("addLong", (long value) => value + 1);

        var gql = new QueryRequest { Query = "mutation { addLong(value: 100) }" };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(101, Convert.ToInt32(result.Data!["addLong"]!));
    }

    [Fact]
    public void TestMutationWithULongArgument()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<ulong>("ULong", "A 64-bit unsigned integer");

        schema.Mutation().Add("addULong", (ulong value) => value + 1);

        var gql = new QueryRequest { Query = "mutation { addULong(value: 100) }" };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal(101, Convert.ToInt32(result.Data!["addULong"]!));
    }

    [Fact]
    public void TestBlockStringWithIndentation()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("echo", (string value) => value);

        var gql = new QueryRequest
        {
            Query =
                @"mutation {
                echo(value: """"""
                    Hello,
                      World!
                    Yours,
                      GraphQL.
                """""")
            }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal("Hello,\n  World!\nYours,\n  GraphQL.", result.Data!["echo"]);
    }

    [Fact]
    public void TestBlockStringWithLeadingAndTrailingEmptyLines()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("echo", (string value) => value);

        var gql = new QueryRequest
        {
            Query =
                @"mutation {
                echo(value: """"""

                    Content here

                """""")
            }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal("Content here", result.Data!["echo"]);
    }

    [Fact]
    public void TestBlockStringEmpty()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("echo", (string value) => value);

        var gql = new QueryRequest { Query = "mutation {\n                echo(value: \"\"\"\"\"\")\n            }" };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal("", result.Data!["echo"]);
    }

    [Fact]
    public void TestBlockStringNoIndentRemoval()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("echo", (string value) => value);

        var gql = new QueryRequest
        {
            Query =
                @"mutation {
                echo(value: """"""No indent
Still no indent"""""")
            }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal("No indent\nStill no indent", result.Data!["echo"]);
    }

    [Fact]
    public void TestBlockStringWithEscapedQuotes()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("echo", (string value) => value);

        var gql = new QueryRequest { Query = "mutation {\n                echo(value: \"\"\"He said \\\"\\\"\\\"Hello\\\"\\\"\\\"\"\"\")\n            }" };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal("He said \"\"\"Hello\"\"\"", result.Data!["echo"]);
    }

    [Fact]
    public void TestBlockStringWithCarriageReturns()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("echo", (string value) => value);

        var gql = new QueryRequest { Query = "mutation {\n                echo(value: \"\"\"\r\n    Line1\r\n    Line2\r\n\"\"\") \n            }" };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal("Line1\nLine2", result.Data!["echo"]);
    }

    [Fact]
    public void TestBlockStringWithLeadingAndTrailingEmptyLinesAndIndentation()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("echo", (string value) => value);

        var gql = new QueryRequest
        {
            Query =
                @"mutation {
                echo(value: """"""

                    Content here
                        And here

                    Here

                """""")
            }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Equal("Content here\n    And here\n\nHere", result.Data!["echo"]);
    }
}
