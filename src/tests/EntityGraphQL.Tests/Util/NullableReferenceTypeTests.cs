using System.Collections.Generic;
using EntityGraphQL.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EntityGraphQL.Tests.Util;

public class NullableReferenceTypeTests
{
    public class Test { }

    // Helper method to compare JSON objects semantically (ignoring property order)
    private static void AssertJsonEqual(string expectedJson, object actual)
    {
        var expectedToken = JToken.Parse(expectedJson);
        var actualToken = JToken.Parse(JsonConvert.SerializeObject(actual));
        Assert.True(JToken.DeepEquals(expectedToken, actualToken), $"Expected: {expectedToken}\nActual: {actualToken}");
    }

    public class WithoutNullableRefEnabled
    {
        public int NonNullableInt { get; set; }
        public int? NullableInt { get; set; }
        public string Nullable { get; set; } = string.Empty;
        public IEnumerable<Test> Tests { get; set; } = [];

        public IEnumerable<Test> NullableMethod()
        {
            return null!;
        }
    }

    [Fact]
    public void TestNullableWithoutNullableRefEnabled()
    {
        var schema = SchemaBuilder.FromObject<WithoutNullableRefEnabled>();
        var schemaString = schema.ToGraphQLSchemaString();

        Assert.Contains(@"nonNullableInt: Int!", schemaString);
        Assert.Contains(@"nullableInt: Int", schemaString);
        Assert.Contains(@"nullable: String", schemaString);
        Assert.Contains(@"tests: [Test!]", schemaString);
    }

#nullable enable
    public class WithNullableRefEnabled
    {
        public int NonNullableInt { get; set; }
        public int? NullableInt { get; set; }
        public string NonNullable { get; set; } = "";
        public string? Nullable { get; set; }
        public IEnumerable<Test> Tests { get; set; } = new List<Test>();
        public IEnumerable<Test>? Tests2 { get; set; }
        public IEnumerable<Test?> Tests3 { get; set; } = new List<Test>();
        public IEnumerable<Test?>? Tests4 { get; set; }

        public IEnumerable<Test> NonNullableMethod()
        {
            return null!;
        }

        public IEnumerable<Test?> NullableMethod()
        {
            return null!;
        }
    }

#nullable restore

    [Fact]
    public void TestNullableWithNullableRefEnabled()
    {
        var schema = SchemaBuilder.FromObject<WithNullableRefEnabled>();
        var schemaString = schema.ToGraphQLSchemaString();

        Assert.Contains(@"nonNullableInt: Int!", schemaString);
        Assert.Contains(@"nullableInt: Int", schemaString);
        Assert.Contains(@"nullable: String", schemaString);
        Assert.Contains(@"nonNullable: String!", schemaString);

        //public IEnumerable<Test> Tests { get; set; } = new List<Test>();
        Assert.Contains("tests: [Test!]!", schemaString);
        //public IEnumerable<Test>? Tests2 { get; set; }
        Assert.Contains("tests2: [Test!]", schemaString);
        Assert.DoesNotContain("tests2: [Test!]!", schemaString);
        //public IEnumerable<Test?> Tests3 { get; set; } = new List<Test>();
        Assert.Contains("tests3: [Test]!", schemaString);
        //public IEnumerable<Test?>? Tests4 { get; set; }
        Assert.Contains("tests4: [Test]", schemaString);
        Assert.DoesNotContain("tests4: [Test]!", schemaString);

        var gql = new QueryRequest
        {
            Query =
                @"
                  query {
                    __type(name: ""Query"") {                        
                        fields {
                            name
                            type  { 
                                name
                                kind
                                ofType {
                                    name
                                    kind
                                }
                            }
                            args {
                                name 
                                type { name kind }
                            }
                        }
                    }
                  }
                ",
        };

        var res = schema.ExecuteRequestWithContext(gql, new WithNullableRefEnabled(), null, null);
        Assert.Null(res.Errors);

        var type = (dynamic)res.Data!["__type"]!;

        AssertJsonEqual(@"{""name"":""nonNullableInt"",""type"":{""name"":null,""kind"":""NON_NULL"",""ofType"":{""name"":""Int"",""kind"":""SCALAR""}},""args"":[]}", type.fields[0]);
        AssertJsonEqual(@"{""name"":""nullableInt"",""type"":{""name"":""Int"",""kind"":""SCALAR"",""ofType"":null},""args"":[]}", type.fields[1]);
        AssertJsonEqual(@"{""name"":""nullable"",""type"":{""name"":""String"",""kind"":""SCALAR"",""ofType"":null},""args"":[]}", type.fields[3]);
        AssertJsonEqual(@"{""name"":""nonNullable"",""type"":{""name"":null,""kind"":""NON_NULL"",""ofType"":{""name"":""String"",""kind"":""SCALAR""}},""args"":[]}", type.fields[2]);

        AssertJsonEqual(@"{""name"":""tests"",""type"":{""name"":null,""kind"":""NON_NULL"",""ofType"":{""name"":null,""kind"":""LIST""}},""args"":[]}", type.fields[4]);
        AssertJsonEqual(@"{""name"":""tests2"",""type"":{""name"":null,""kind"":""LIST"",""ofType"":{""name"":""Test"",""kind"":""OBJECT""}},""args"":[]}", type.fields[5]);
        AssertJsonEqual(@"{""name"":""tests3"",""type"":{""name"":null,""kind"":""NON_NULL"",""ofType"":{""name"":null,""kind"":""LIST""}},""args"":[]}", type.fields[6]);
        AssertJsonEqual(@"{""name"":""tests4"",""type"":{""name"":null,""kind"":""LIST"",""ofType"":{""name"":""Test"",""kind"":""OBJECT""}},""args"":[]}", type.fields[7]);
    }
}
