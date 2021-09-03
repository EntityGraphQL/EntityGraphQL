using System.Linq;
using System.Text;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

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

                var query = JsonConvert.DeserializeObject<QueryRequest>(json);
                var schema = context.RequestServices.GetService<SchemaProvider<TQueryType>>();
                var schemaContext = context.RequestServices.GetService<TQueryType>();
                var data = await schema.ExecuteQueryAsync(query, schemaContext, context.RequestServices, context.User.Identities.FirstOrDefault(), options);
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                serializerSettings.Converters.Add(new StringEnumConverter());
                context.Response.ContentType = "application/json; charset=utf-8";
                var jsonResult = JsonConvert.SerializeObject(data, serializerSettings);
                var jsonBytes = Encoding.UTF8.GetBytes(jsonResult);
                context.Response.ContentLength = jsonBytes.Length;
                await context.Response.Body.WriteAsync(jsonBytes);
            });


            return builder;
        }
    }
}