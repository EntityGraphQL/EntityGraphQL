using EntityGraphQL.AspNet.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace EntityGraphQL.AspNet.Tests
{
    public class RuntimeTypeJsonConverterTests
    {
        public class BaseClass
        {
            public int Id { get; set; }
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
            Assert.Equal("{\"Id\":1}", result);

            var options = new JsonSerializerOptions();
            options.Converters.Add(new RuntimeTypeJsonConverter<object>());
            result = System.Text.Json.JsonSerializer.Serialize(item, options);
            Assert.Equal("{\"Name\":\"Fred\",\"Id\":1}", result);

            options = new JsonSerializerOptions();
            options.Converters.Add(new RuntimeTypeJsonConverter<object>());
            options.IncludeFields = true;
            result = System.Text.Json.JsonSerializer.Serialize(item, options);
            Assert.Equal("{\"Name\":\"Fred\",\"Id\":1,\"NameField\":\"Included\"}", result);

            var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
            var memoryStream = new MemoryStream();
            graphqlResponseSerializer.SerializeAsync(memoryStream, item);
            result = Encoding.ASCII.GetString(memoryStream.ToArray());
            Assert.Equal("{\"Name\":\"Fred\",\"Id\":1,\"NameField\":\"Included\"}", result);
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
                          new SubClass() { Id = 2, Name = "Wilma", NameField = null }
                    }
                }
            });
            
            var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
            var memoryStream = new MemoryStream();
            graphqlResponseSerializer.SerializeAsync(memoryStream, item);
            var result = Encoding.ASCII.GetString(memoryStream.ToArray());
            Assert.Equal("{\"data\":{\"users\":[{\"Name\":\"Fred\",\"Id\":1,\"NameField\":\"Included\"},{\"Name\":\"Wilma\",\"Id\":2}]}}", result);
        }

    }
}

