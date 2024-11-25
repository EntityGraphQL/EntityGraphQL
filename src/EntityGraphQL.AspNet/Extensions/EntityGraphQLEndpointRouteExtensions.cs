using System;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.AspNet;

public static class EntityGraphQLEndpointRouteExtensions
{
    private const string APP_JSON_TYPE_START = "application/json";

    // private const string APP_GQL_TYPE_START = "application/graphql-response+json";

    public static IEndpointRouteBuilder MapGraphQL<TQueryType>(
        this IEndpointRouteBuilder builder,
        string path = "graphql",
        ExecutionOptions? options = null,
        Action<IEndpointConventionBuilder>? configureEndpoint = null
    )
    {
        path = path.TrimEnd('/');
        var requestPipeline = builder.CreateApplicationBuilder();
        var postEndpoint = builder.MapPost(
            path,
            async context =>
            {
                // var acceptedContentType = context.Request.Headers.Accept;
                // https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md
                // if (!requestedContentType.Contains(APP_JSON_TYPE) && !requestedContentType.Contains(APP_GQL_TYPE))
                // {
                //     context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
                //     return;
                // }
                if (context.Request.ContentType?.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) == false)
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
                var query = await deserializer.DeserializeAsync(context.Request.Body);

                var schema =
                    context.RequestServices.GetService<SchemaProvider<TQueryType>>()
                    ?? throw new InvalidOperationException(
                        "No SchemaProvider<TQueryType> found in the service collection. Make sure you set up your Startup.ConfigureServices() to call AddGraphQLSchema<TQueryType>()."
                    );
                var data = await schema.ExecuteRequestAsync(query, context.RequestServices, context.User, options);
                // var requestedType = acceptedContentType.FirstOrDefault(t => t?.StartsWith(APP_JSON_TYPE_START, StringComparison.InvariantCulture) == true || t?.StartsWith(APP_GQL_TYPE_START, StringComparison.InvariantCulture) == true);
                // TODO 2025-01-01. if this goes forward https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md
                // change default to application/graphql-response+json; charset=utf-8
                // context.Response.ContentType = requestedType ?? "application/json; charset=utf-8";
                context.Response.ContentType = "application/json; charset=utf-8";
                if (data.Errors?.Count > 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
                var serializer = context.RequestServices.GetRequiredService<IGraphQLResponseSerializer>();
                await serializer.SerializeAsync(context.Response.Body, data);
            }
        );

        configureEndpoint?.Invoke(postEndpoint);

        return builder;
    }
}
