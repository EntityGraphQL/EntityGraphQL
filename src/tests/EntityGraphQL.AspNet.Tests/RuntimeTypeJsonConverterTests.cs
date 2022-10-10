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
        }

        [Fact]
        public void SerializeSubTypes()
        {
            BaseClass item = new SubClass() { Id = 1, Name = "Fred" };
            var result = System.Text.Json.JsonSerializer.Serialize(item);
            Assert.Equal("{\"Id\":1}", result);

            var options = new JsonSerializerOptions();
            options.Converters.Add(new RuntimeTypeJsonConverter<object>());
            result = System.Text.Json.JsonSerializer.Serialize(item, item.GetType(), options);
            Assert.Equal("{\"Name\":\"Fred\",\"Id\":1}", result);

            var graphqlResponseSerializer = new DefaultGraphQLResponseSerializer();
            var memoryStream = new MemoryStream();
            graphqlResponseSerializer.SerializeAsync(memoryStream, item);
            result = Encoding.ASCII.GetString(memoryStream.ToArray());
            Assert.Equal("{\"Name\":\"Fred\",\"Id\":1}", result);
        }
    }
}
