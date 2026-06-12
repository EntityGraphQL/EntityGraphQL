using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using EntityGraphQL.AspNet.Extensions;

namespace EntityGraphQL.AspNet;

/// <summary>
/// Serializes GraphQL responses into a JSON response format.
/// </summary>
public class DefaultGraphQLResponseSerializer : IGraphQLResponseSerializer
{
    private readonly JsonSerializerOptions jsonOptions;

    public DefaultGraphQLResponseSerializer(JsonSerializerOptions? jsonOptions = null)
    {
        this.jsonOptions = jsonOptions ?? CreateDefaultOptions();
    }

    /// <summary>
    /// Builds the default <see cref="JsonSerializerOptions"/> used to serialize GraphQL responses.
    /// Use this as a starting point when you only need to tweak the defaults (e.g. remove the
    /// <see cref="RuntimeTypeJsonConverter"/>) rather than reproducing every setting from scratch:
    /// <code>
    /// var options = DefaultGraphQLResponseSerializer.CreateDefaultOptions();
    /// var converter = options.Converters.FirstOrDefault(c => c is RuntimeTypeJsonConverter);
    /// if (converter != null)
    ///     options.Converters.Remove(converter);
    /// services.AddSingleton&lt;IGraphQLResponseSerializer&gt;(new DefaultGraphQLResponseSerializer(options));
    /// </code>
    /// </summary>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new RuntimeTypeJsonConverter());
        return options;
    }

    public Task SerializeAsync<T>(Stream body, T data) => JsonSerializer.SerializeAsync(body, data, jsonOptions);
}
