using System;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EntityGraphQL.AspNet;

public static class EntityGraphQLEndpointRouteExtensions
{
    private const string APP_JSON_TYPE_START = "application/json";
    private const string APP_GQL_TYPE_START = "application/graphql-response+json";

    private static readonly Action<ILogger, Exception> logResponseError = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, nameof(MapGraphQL)),
        "Error executing or serializing the GraphQL response"
    );

    private static readonly Action<ILogger, int, string, Exception?> logRequestRejected = LoggerMessage.Define<int, string>(
        LogLevel.Debug,
        new EventId(2, nameof(MapGraphQL)),
        "GraphQL request rejected with status {StatusCode}: {Reason}"
    );

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
                var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(EntityGraphQLEndpointRouteExtensions)) ?? NullLogger.Instance;

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
                    logRequestRejected(logger, StatusCodes.Status406NotAcceptable, "Accept header does not allow a JSON GraphQL response", null);
                    context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                    return;
                }
                // checking for ContentType == null is technically a breaking change so only do it for the followSpec case until 6.0
                if (context.Request.ContentType == null || context.Request.ContentType?.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) == false)
                {
                    logRequestRejected(logger, StatusCodes.Status415UnsupportedMediaType, $"Unsupported Content-Type '{context.Request.ContentType}'", null);
                    context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                    return;
                }
                var isChunked = context.Request.Headers.TransferEncoding.Any(h => h is not null && h.Equals("chunked", StringComparison.OrdinalIgnoreCase));

                if (!isChunked && (context.Request.ContentLength == null || context.Request.ContentLength == 0))
                {
                    logRequestRejected(logger, StatusCodes.Status400BadRequest, "Request body is empty", null);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var deserializer = context.RequestServices.GetRequiredService<IGraphQLRequestDeserializer>();
                QueryRequest query;
                try
                {
                    query = await deserializer.DeserializeAsync(context.Request.Body);
                }
                catch (Exception ex)
                {
                    logRequestRejected(logger, StatusCodes.Status400BadRequest, "Failed to deserialize the GraphQL request body", ex);
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

                    // Status codes per the GraphQL over HTTP spec. For application/graphql-response+json the
                    // status reflects the GraphQL outcome (clients should still read the body - the code aids
                    // intermediaries/observability): no data entry means a request error (4xx - the spec
                    // recommends 400/422 per case; we use 400 as the errors don't distinguish parse from
                    // validation), data plus errors is a partial success (294 per the spec), otherwise 200.
                    // For legacy application/json the spec requires 200 for every well-formed request
                    if (context.Response.ContentType.StartsWith(APP_GQL_TYPE_START, StringComparison.InvariantCulture))
                    {
                        if (!gqlResult.HasDataKey)
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        else if (gqlResult.HasErrors())
                            context.Response.StatusCode = 294; // partial success
                        else
                            context.Response.StatusCode = StatusCodes.Status200OK;
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status200OK;
                    }

                    var serializer = context.RequestServices.GetRequiredService<IGraphQLResponseSerializer>();
                    await serializer.SerializeAsync(context.Response.Body, gqlResult);
                }
                catch (Exception ex)
                {
                    // Something went very wrong executing the query or serializing the response.
                    // Surface it via logging so it is not silently swallowed.
                    logResponseError(logger, ex);

                    // Only set the status code if the response has not started; once serialization has
                    // begun writing to the body the headers are already sent and setting the status throws.
                    if (!context.Response.HasStarted)
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return;
                }
            }
        );

        configureEndpoint?.Invoke(postEndpoint);

        return builder;
    }
}
