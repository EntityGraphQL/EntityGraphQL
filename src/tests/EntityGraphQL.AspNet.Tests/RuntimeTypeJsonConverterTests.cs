using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EntityGraphQL.AspNet.Extensions;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.AspNet.Tests;

public class RuntimeTypeJsonConverterTests
{
    public enum Enum
    {
        First,
        Second
    }

    public class BaseClass
    {
        public int Id { get; set; }
        public Enum E { get; set; }
    }

    public class SubClass : BaseClass
    {
        public string Name { get; set; } = "";
        public string? NameField;
    }

    [Fact]
    public void SerializeSubTypes()
    {
        BaseClass item = new SubClass()
        {
            Id = 1,
            Name = "Fred",
            NameField = "Included"
        };
        var result = JsonSerializer.Serialize(item);
        Assert.Equal("{\"Id\":1,\"E\":0}", result);

        var options = new JsonSerializerOptions();
        options.Converters.Add(new RuntimeTypeJsonConverter());
        result = JsonSerializer.Serialize(item, options);
        Assert.Equal("{\"Name\":\"Fred\",\"Id\":1,\"E\":0}", result);

        options = new JsonSerializerOptions();
        options.Converters.Add(new RuntimeTypeJsonConverter());
        options.IncludeFields = true;
        result = JsonSerializer.Serialize(item, options);
        Assert.Equal("{\"Name\":\"Fred\",\"Id\":1,\"E\":0,\"NameField\":\"Included\"}", result);

        // DefaultGraphQLResponseSerializer sets camelCase and includes fields
        var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
        var memoryStream = new MemoryStream();
        graphqlResponseSerializer.SerializeAsync(memoryStream, item);
        result = Encoding.ASCII.GetString(memoryStream.ToArray());
        Assert.Equal("{\"name\":\"Fred\",\"id\":1,\"e\":\"First\",\"nameField\":\"Included\"}", result);
    }

    [Fact]
    public void SerializeOffsetPage()
    {
        var item = new OffsetPage<SubClass>(0, 0, 10) { Items = [] };

        var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
        var memoryStream = new MemoryStream();
        graphqlResponseSerializer.SerializeAsync(memoryStream, item);
        var result = Encoding.ASCII.GetString(memoryStream.ToArray());

        Assert.Equal("{\"items\":[],\"hasPreviousPage\":false,\"hasNextPage\":false,\"totalItems\":0}", result);
    }

    [Fact]
    public void SerializeDynamicOffsetPage()
    {
        dynamic item = new { items = new List<SubClass>(), hasNextPage = false };

        var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
        var memoryStream = new MemoryStream();
        graphqlResponseSerializer.SerializeAsync(memoryStream, item);
        var result = Encoding.ASCII.GetString(memoryStream.ToArray());

        Assert.Equal("{\"items\":[],\"hasNextPage\":false}", result);
    }

    struct Test
    {
        public List<SubClass> items;
        public bool hasNextPage;
    }

    [Fact]
    public void SerializeStruct()
    {
        dynamic item = new Test { items = new List<SubClass>(), hasNextPage = false };

        var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
        var memoryStream = new MemoryStream();
        graphqlResponseSerializer.SerializeAsync(memoryStream, item);
        var result = Encoding.ASCII.GetString(memoryStream.ToArray());

        Assert.Equal("{\"items\":[],\"hasNextPage\":false}", result);
    }

    [Fact]
    public void SerializeDictionary()
    {
        dynamic item = new Dictionary<string, object>()
        {
            {
                "test",
                new Test { items = new List<SubClass>(), hasNextPage = false }
            }
        };

        var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
        var memoryStream = new MemoryStream();
        graphqlResponseSerializer.SerializeAsync(memoryStream, item);
        var result = Encoding.ASCII.GetString(memoryStream.ToArray());

        Assert.Equal("{\"test\":{\"items\":[],\"hasNextPage\":false}}", result);
    }

    [Fact]
    public void SerializeBoolean()
    {
        var item = new { value = false };

        var options = new JsonSerializerOptions { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new RuntimeTypeJsonConverter());

        var result = JsonSerializer.Serialize(item, options);
        Assert.Equal("{\"value\":false}", result);
    }

    [Fact]
    public void SerializeStringWithDifferentEncoder()
    {
        var item = new { value = "Nick's coffee" };

        var options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new RuntimeTypeJsonConverter());

        var result = JsonSerializer.Serialize(item, options);
        Assert.Equal("{\"value\":\"Nick's coffee\"}", result);
    }

    [Fact]
    public void SerializeString()
    {
        var item = new { value = "Nick's coffee" };

        var options = new JsonSerializerOptions { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new RuntimeTypeJsonConverter());

        var result = JsonSerializer.Serialize(item, options);
        Assert.Equal("{\"value\":\"Nick\\u0027s coffee\"}", result);
    }

    [Fact]
    public void SerializeNestedObject()
    {
        var item = new { child = new { value = 3.4 } };

        var options = new JsonSerializerOptions { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new RuntimeTypeJsonConverter());
        options.IncludeFields = true;

        var result = JsonSerializer.Serialize(item, options);
        Assert.Equal("{\"child\":{\"value\":3.4}}", result);
    }

    [Fact]
    public void SerializeQueryResult()
    {
        var item = new QueryResult();
        item.SetData(
            new Dictionary<string, object?>()
            {
                {
                    "users",
                    new List<SubClass>()
                    {
                        new SubClass()
                        {
                            Id = 1,
                            Name = "Fred",
                            NameField = "Included"
                        },
                        new SubClass()
                        {
                            Id = 2,
                            Name = "Wilma",
                            NameField = null,
                            E = Enum.Second
                        }
                    }
                }
            }
        );

        var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
        var memoryStream = new MemoryStream();
        graphqlResponseSerializer.SerializeAsync(memoryStream, item);
        var result = Encoding.ASCII.GetString(memoryStream.ToArray());
        Assert.Equal("{\"data\":{\"users\":[{\"name\":\"Fred\",\"id\":1,\"e\":\"First\",\"nameField\":\"Included\"},{\"name\":\"Wilma\",\"id\":2,\"e\":\"Second\",\"nameField\":null}]}}", result);
    }
}
