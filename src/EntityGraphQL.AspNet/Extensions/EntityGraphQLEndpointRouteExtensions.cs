using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.AspNet
{
    public static class EntityGraphQLEndpointRouteExtensions
    {
        public static IEndpointRouteBuilder MapGraphQL<TQueryType>(this IEndpointRouteBuilder builder, string path = "graphql", ExecutionOptions? options = null)
        {
            path = path.TrimEnd('/');
            var requestPipeline = builder.CreateApplicationBuilder();
            builder.MapPost(path, async context =>
            {
                if (context.Request.ContentType != "application/json")
                {
                    context.Response.StatusCode = 415;
                    return;
                }
                if (context.Request.ContentLength == null || context.Request.ContentLength == 0)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    IncludeFields = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                jsonOptions.Converters.Add(new JsonStringEnumConverter());
                var query = await JsonSerializer.DeserializeAsync<QueryRequest>(context.Request.Body, jsonOptions);
                var schema = context.RequestServices.GetService<SchemaProvider<TQueryType>>();
                if (schema == null)
                    throw new InvalidOperationException("No SchemaProvider<TQueryType> found in the service collection. Make sure you set up your Startup.ConfigureServices() to call AddGraphQLSchema<TQueryType>().");

                var schemaContext = context.RequestServices.GetService<TQueryType>();
                if (schemaContext == null)
                    throw new InvalidOperationException("No schema context was found in the service collection. Make sure the TQueryType used with MapGraphQL<TQueryType>() is registered in the service collection.");

                var data = await schema.ExecuteRequestAsync(query, schemaContext, context.RequestServices, context.User, options);
                context.Response.ContentType = "application/json; charset=utf-8";
                await JsonSerializer.SerializeAsync(context.Response.Body, data, jsonOptions);
            });

            return builder;
        }
    }
}