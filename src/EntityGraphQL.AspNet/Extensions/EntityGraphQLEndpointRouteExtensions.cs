using System;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.AspNet;

public static class EntityGraphQLEndpointRouteExtensions
{
    private const string APP_JSON_TYPE_START = "application/json";
    private const string APP_GQL_TYPE_START = "application/graphql-response+json";

    /// <summary>
    /// Add the GraphQL endpoint to the route builder
    /// </summary>
    /// <typeparam name="TQueryType">The base query type to build the schema from</typeparam>
    /// <param name="builder">The IEndpointRouteBuilder</param>
    /// <param name="path">The path to create the route at. Defaults to `graphql`</param>
    /// <param name="options">ExecutionOptions to use when executing queries</param>
    /// <param name="configureEndpoint">Callback to continue modifying the endpoint via the IEndpointConventionBuilder interface</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IEndpointRouteBuilder MapGraphQL<TQueryType>(
        this IEndpointRouteBuilder builder,
        string path = "graphql",
        ExecutionOptions? options = null,
        Action<IEndpointConventionBuilder>? configureEndpoint = null
    )
    {
        path = path.TrimEnd('/');
        var postEndpoint = builder.MapPost(
            path,
            async context =>
            {
                var acceptValues = context.Request.GetTypedHeaders().Accept;
                var sorted = acceptValues.OrderByDescending(h => h.Quality ?? 1.0).ToList();

                // https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md
                // "May reply with error if not supplied" choosing not to
                if (
                    acceptValues.Count != 0
                    && !sorted.Any(h => h.MediaType.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) == true)
                    && !sorted.Any(h => h.MediaType.StartsWith(APP_GQL_TYPE_START, StringComparison.InvariantCulture) == true)
                    && !sorted.Any(h => h.MediaType.StartsWith("*/*", StringComparison.InvariantCulture) == true)
                    && !sorted.Any(h => h.MediaType.StartsWith("application/*", StringComparison.InvariantCulture) == true)
                )
                {
                    context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                    return;
                }
                // checking for ContentType == null is technically a breaking change so only do it for the followSpec case until 6.0
                if (context.Request.ContentType == null || context.Request.ContentType?.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) == false)
                {
                    context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                    return;
                }
                var isChunked = context.Request.Headers.TransferEncoding.Any(h => h is not null && h.Equals("chunked", StringComparison.OrdinalIgnoreCase));

                if (!isChunked && (context.Request.ContentLength == null || context.Request.ContentLength == 0))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var deserializer = context.RequestServices.GetRequiredService<IGraphQLRequestDeserializer>();
                QueryRequest query;
                try
                {
                    query = await deserializer.DeserializeAsync(context.Request.Body);
                }
                catch (Exception)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var schema =
                    context.RequestServices.GetService<SchemaProvider<TQueryType>>()
                    ?? throw new InvalidOperationException(
                        "No SchemaProvider<TQueryType> found in the service collection. Make sure you set up your Startup.ConfigureServices() to call AddGraphQLSchema<TQueryType>()."
                    );
                var requestedType = sorted
                    .Where(t => t != null)
                    .FirstOrDefault(t =>
                        t.MediaType.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) || t.MediaType.StartsWith(APP_GQL_TYPE_START, StringComparison.InvariantCulture)
                    )
                    ?.MediaType.ToString();

                try
                {
                    var gqlResult = await schema.ExecuteRequestAsync(query, context.RequestServices, context.User, options, context.RequestAborted);

                    context.Response.ContentType = requestedType ?? $"{APP_GQL_TYPE_START}; charset=utf-8";

                    // Per GraphQL over HTTP spec: GraphQL errors should return 200 with error details
                    context.Response.StatusCode = StatusCodes.Status200OK;

                    var serializer = context.RequestServices.GetRequiredService<IGraphQLResponseSerializer>();
                    await serializer.SerializeAsync(context.Response.Body, gqlResult);
                }
                catch (Exception)
                {
                    // something went very wrong
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return;
                }
            }
        );

        configureEndpoint?.Invoke(postEndpoint);

        return builder;
    }
}
