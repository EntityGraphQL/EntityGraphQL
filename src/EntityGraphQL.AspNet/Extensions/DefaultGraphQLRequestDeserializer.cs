using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EntityGraphQL.AspNet;

/// <summary>
/// Deserializes a JSON GraphQL request into a QueryRequest object.
/// </summary>
public class DefaultGraphQLRequestDeserializer : IGraphQLRequestDeserializer
{
    private readonly JsonSerializerOptions jsonOptions;

    public DefaultGraphQLRequestDeserializer(JsonSerializerOptions? jsonOptions = null)
    {
        this.jsonOptions = jsonOptions ?? CreateDefaultOptions();
    }

    /// <summary>
    /// Builds the default <see cref="JsonSerializerOptions"/> used to deserialize GraphQL requests.
    /// Use this as a starting point when you only need to tweak the defaults rather than reproducing
    /// every setting from scratch.
    /// </summary>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        return options;
    }

    public async Task<QueryRequest> DeserializeAsync(Stream body)
    {
        var query = await JsonSerializer.DeserializeAsync<QueryRequest>(body, jsonOptions);
        return query ?? throw new ArgumentNullException(nameof(body), $"Request body could not be deserialized as JSON into QueryRequest");
    }
}
