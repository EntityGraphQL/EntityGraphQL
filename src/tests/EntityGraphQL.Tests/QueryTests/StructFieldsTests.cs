using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class StructFieldsTests
{
    [Fact]
    public void TestStructFields()
    {
        var schema = SchemaBuilder.FromObject<TestData>();

        schema.AddType<TestStruct>(
            "TestStruct",
            "A test struct",
            (b) =>
            {
                b.AddField("name", (c) => c.Name, "The name of the struct");
                b.AddField("age", (c) => c.Age, "The age of the struct");
            }
        );

        var query = new QueryRequest
        {
            Query =
                @"{
                structField {
                    name
                    age
                }
            }"
        };

        var data = new TestData
        {
            StructField = new TestStruct { Name = "Test", Age = 10 }
        };

        var res = schema.ExecuteRequestWithContext(query, data, null, null);

        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        dynamic field = res.Data["structField"]!;
        Assert.Equal("Test", field.name);
        Assert.Equal(10, field.age);
    }

    [Fact]
    public void TestNullableStructFields()
    {
        var schema = SchemaBuilder.FromObject<TestData>();

        schema.AddType<TestStruct>(
            "TestStruct",
            "A test struct",
            (b) =>
            {
                b.AddField("name", (c) => c.Name, "The name of the struct");
                b.AddField("age", (c) => c.Age, "The age of the struct");
            }
        );

        var query = new QueryRequest
        {
            Query =
                @"{
                nullableStructField {
                    name
                    age
                }
            }"
        };

        var data = new TestData
        {
            NullableStructField = new TestStruct { Name = "Test", Age = 10 }
        };

        var res = schema.ExecuteRequestWithContext(query, data, null, null);

        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        dynamic field = res.Data["nullableStructField"]!;
        Assert.Equal("Test", field.name);
        Assert.Equal(10, field.age);
    }

    public class TestData
    {
        public TestStruct StructField { get; set; }
        public TestStruct? NullableStructField { get; set; }
    }

    public struct TestStruct
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
