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
    /// <param name="followSpec">Defaults to false. If true it will return status code 200 for queries with
    /// errors as per https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IEndpointRouteBuilder MapGraphQL<TQueryType>(
        this IEndpointRouteBuilder builder,
        string path = "graphql",
        ExecutionOptions? options = null,
        Action<IEndpointConventionBuilder>? configureEndpoint = null,
        bool followSpec = false
    )
    {
        path = path.TrimEnd('/');
        var postEndpoint = builder.MapPost(
            path,
            async context =>
            {
                var acceptedContentType = context.Request.Headers.Accept;
                if (followSpec)
                {
                    // https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md
                    // "May reply with error if not supplied" choosing not to
                    if (
                        acceptedContentType.Count > 0
                        && !acceptedContentType.Any(h => h?.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) == true)
                        && !acceptedContentType.Any(h => h?.StartsWith(APP_GQL_TYPE_START, StringComparison.InvariantCulture) == true)
                    )
                    {
                        context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                        return;
                    }
                }
                // checking for ContentType == null is technically a breaking change so only do it for the followSpec case until 6.0
                if ((followSpec && context.Request.ContentType == null) || context.Request.ContentType?.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) == false)
                {
                    context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                    return;
                }
                if (context.Request.ContentLength == null || context.Request.ContentLength == 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var deserializer = context.RequestServices.GetRequiredService<IGraphQLRequestDeserializer>();
                try
                {
                    var query = await deserializer.DeserializeAsync(context.Request.Body);

                    var schema =
                        context.RequestServices.GetService<SchemaProvider<TQueryType>>()
                        ?? throw new InvalidOperationException(
                            "No SchemaProvider<TQueryType> found in the service collection. Make sure you set up your Startup.ConfigureServices() to call AddGraphQLSchema<TQueryType>()."
                        );
                    var gqlResult = await schema.ExecuteRequestAsync(query, context.RequestServices, context.User, options);

                    if (followSpec)
                    {
                        var requestedType = acceptedContentType.FirstOrDefault(t =>
                            t?.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) == true || t?.StartsWith(APP_GQL_TYPE_START, StringComparison.InvariantCulture) == true
                        );
                        context.Response.ContentType = requestedType ?? $"{APP_GQL_TYPE_START}; charset=utf-8";
                    }
                    else
                    {
                        context.Response.ContentType = $"{APP_JSON_TYPE_START}; charset=utf-8";
                    }

                    if (gqlResult.Errors?.Count > 0)
                    {
                        // TODO: change with 6.0. This is here as changing how errors are thrown would be a breaking change
                        // But following the spec this is not a valid request and should be a 400
                        if (followSpec && gqlResult.Errors.Count == 1 && gqlResult.Errors[0].Message == "Please provide a persisted query hash or a query string")
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            return;
                        }
                        context.Response.StatusCode = followSpec ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest;
                    }
                    var serializer = context.RequestServices.GetRequiredService<IGraphQLResponseSerializer>();
                    await serializer.SerializeAsync(context.Response.Body, gqlResult);
                }
                catch (Exception)
                {
                    // only exceptions we should get are ones that mean the request is invalid, e.g. deserialization errors
                    // all other graphql specific errors should be in the response data
                    if (followSpec)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }
                    else
                    {
                        // keep the old behavior for v 5.x
                        throw;
                    }
                }
            }
        );

        configureEndpoint?.Invoke(postEndpoint);

        return builder;
    }
}
