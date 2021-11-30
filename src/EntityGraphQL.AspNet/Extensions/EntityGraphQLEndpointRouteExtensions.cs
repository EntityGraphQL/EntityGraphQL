using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.AspNet.Extensions
{
    public static class EntityGraphQLEndpointRouteExtensions
    {
        public static IEndpointRouteBuilder MapGraphQL<TQueryType>(this IEndpointRouteBuilder builder, string path = "graphql", ExecutionOptions options = null)
        {
            path = path.TrimEnd('/');
            var requestPipeline = builder.CreateApplicationBuilder();
            builder.MapPost(path, async context =>
            {
                var buffer = new byte[context.Request.ContentLength.Value];
                await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
                var json = Encoding.UTF8.GetString(buffer);

                var jsonOptions = new JsonSerializerOptions
                {
                    IncludeFields = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                jsonOptions.Converters.Add(new JsonStringEnumConverter());
                var query = JsonSerializer.Deserialize<QueryRequest>(json, jsonOptions);
                var schema = context.RequestServices.GetService<SchemaProvider<TQueryType>>();
                var schemaContext = context.RequestServices.GetService<TQueryType>();
                var data = await schema.ExecuteQueryAsync(query, schemaContext, context.RequestServices, context.User.Identities.FirstOrDefault(), options);
                context.Response.ContentType = "application/json; charset=utf-8";
                var jsonResult = JsonSerializer.Serialize(data, jsonOptions);
                var jsonBytes = Encoding.UTF8.GetBytes(jsonResult);
                context.Response.ContentLength = jsonBytes.Length;
                await context.Response.Body.WriteAsync(jsonBytes);
            });

            return builder;
        }
    }
}