using System;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.AspNet
{
    public static class EntityGraphQLEndpointRouteExtensions
    {
        public static IEndpointRouteBuilder MapGraphQL<TQueryType>(this IEndpointRouteBuilder builder, string path = "graphql", ExecutionOptions? options = null, Action<IEndpointConventionBuilder>? configureEndpoint = null)
        {
            path = path.TrimEnd('/');
            var requestPipeline = builder.CreateApplicationBuilder();
            var postEndpoint = builder.MapPost(path, async context =>
            {
                if (context.Request.ContentType?.StartsWith("application/json", StringComparison.InvariantCulture) == false)
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

                var schema = context.RequestServices.GetService<SchemaProvider<TQueryType>>() ?? throw new InvalidOperationException("No SchemaProvider<TQueryType> found in the service collection. Make sure you set up your Startup.ConfigureServices() to call AddGraphQLSchema<TQueryType>().");
                var data = await schema.ExecuteRequestAsync(query, context.RequestServices, context.User, options);
                context.Response.ContentType = "application/json; charset=utf-8";
                if (data.Errors?.Count > 0)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
                var serializer = context.RequestServices.GetRequiredService<IGraphQLResponseSerializer>();
                await serializer.SerializeAsync(context.Response.Body, data);
            });

            configureEndpoint?.Invoke(postEndpoint);

            return builder;
        }
    }
}