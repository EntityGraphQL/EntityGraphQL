using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EntityGraphQL.AspNet
{
    /// <summary>
    /// Deserializes a JSON GraphQL request into a QueryRequest object.
    /// </summary>
    public class DefaultGraphQLRequestDeserializer : IGraphQLRequestDeserializer
    {
        private readonly JsonSerializerOptions jsonOptions;

        public DefaultGraphQLRequestDeserializer(JsonSerializerOptions? jsonOptions = null)
        {
            if (jsonOptions != null)
                this.jsonOptions = jsonOptions;
            else
            {
                this.jsonOptions = new JsonSerializerOptions
                {
                    IncludeFields = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                this.jsonOptions.Converters.Add(new JsonStringEnumConverter());
            }
        }

        public async Task<QueryRequest> DeserializeAsync(Stream body)
        {
            var query = await JsonSerializer.DeserializeAsync<QueryRequest>(body, jsonOptions);
            return query ?? throw new ArgumentNullException(nameof(body), $"Request body could not be deserialized as JSON into QueryRequest");
        }
    }
}