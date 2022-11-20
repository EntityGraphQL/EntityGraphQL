using EntityGraphQL.AspNet.Extensions;
using EntityGraphQL.Schema.FieldExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace EntityGraphQL.AspNet.Tests
{
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
            BaseClass item = new SubClass() { Id = 1, Name = "Fred", NameField = "Included" };
            var result = System.Text.Json.JsonSerializer.Serialize(item);
            Assert.Equal("{\"Id\":1,\"E\":0}", result);

            var options = new JsonSerializerOptions();
            options.Converters.Add(new RuntimeTypeJsonConverter());
            result = System.Text.Json.JsonSerializer.Serialize(item, options);
            Assert.Equal("{\"Name\":\"Fred\",\"Id\":1,\"E\":0}", result);

            options = new JsonSerializerOptions();
            options.Converters.Add(new RuntimeTypeJsonConverter());
            options.IncludeFields = true;
            result = System.Text.Json.JsonSerializer.Serialize(item, options);
            Assert.Equal("{\"Name\":\"Fred\",\"Id\":1,\"E\":0,\"NameField\":\"Included\"}", result);

            var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
            var memoryStream = new MemoryStream();
            graphqlResponseSerializer.SerializeAsync(memoryStream, item);
            result = Encoding.ASCII.GetString(memoryStream.ToArray());
            Assert.Equal("{\"Name\":\"Fred\",\"Id\":1,\"E\":\"First\",\"NameField\":\"Included\"}", result);
        }

        [Fact]
        public void SerializeOffsetPage()
        {
            var item = new OffsetPage<SubClass>(0, 0, 10) { Items = new List<SubClass>() };

            var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
            var memoryStream = new MemoryStream();
            graphqlResponseSerializer.SerializeAsync(memoryStream, item);
            var result = Encoding.ASCII.GetString(memoryStream.ToArray());

            Assert.Equal("{\"Items\":[],\"HasPreviousPage\":false,\"HasNextPage\":false,\"TotalItems\":0}", result);
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
            dynamic item = new Dictionary<string, object>() { { "test", new Test { items = new List<SubClass>(), hasNextPage = false } } };

            var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
            var memoryStream = new MemoryStream();
            graphqlResponseSerializer.SerializeAsync(memoryStream, item);
            var result = Encoding.ASCII.GetString(memoryStream.ToArray());

            Assert.Equal("{\"test\":{\"items\":[],\"hasNextPage\":false}}", result);
        }

        [Fact]
        public void SerializeBoolean()
        {
            var item = new { value = false};

            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new RuntimeTypeJsonConverter());

            var result = System.Text.Json.JsonSerializer.Serialize(item, options);
            Assert.Equal("{\"value\":false}", result);
        }


        [Fact]
        public void SerializeNestedObject()
        {
            var item = new { child = new { value = 3.4 } };

            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new RuntimeTypeJsonConverter());
            options.IncludeFields = true;

            var result = System.Text.Json.JsonSerializer.Serialize(item, options);
            Assert.Equal("{\"child\":{\"value\":3.4}}", result);
        }

        [Fact]
        public void SerializeQueryResult()
        {
            var item = new QueryResult();
            item.SetData(new Dictionary<string, object?>()
            {
                { 
                    "users",
                    new List<SubClass>() {
                          new SubClass() { Id = 1, Name = "Fred", NameField = "Included" },
                          new SubClass() { Id = 2, Name = "Wilma", NameField = null, E = Enum.Second }
                    }
                }
            });
            
            var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
            var memoryStream = new MemoryStream();
            graphqlResponseSerializer.SerializeAsync(memoryStream, item);
            var result = Encoding.ASCII.GetString(memoryStream.ToArray());
            Assert.Equal("{\"data\":{\"users\":[{\"Name\":\"Fred\",\"Id\":1,\"E\":\"First\",\"NameField\":\"Included\"},{\"Name\":\"Wilma\",\"Id\":2,\"E\":\"Second\"}]}}", result);
        }

    }
}

