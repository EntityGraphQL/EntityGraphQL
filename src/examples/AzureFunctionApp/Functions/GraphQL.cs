using demo;
using demo.Infrastructure;
using EntityGraphQL;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AzureFunctionApp.Functions
{
    public class GraphQL
    {
        private readonly DemoContext _dbContext;
        private readonly SchemaProvider<DemoContext> _schemaProvider;
        private readonly IServiceProvider? _serviceProvider;
        private readonly ClaimsPrincipalAccessor? _claimsPrincipalAccessor;

        public GraphQL(DemoContext dbContext, SchemaProvider<DemoContext> schemaProvider, IServiceProvider? serviceProvider, ClaimsPrincipalAccessor? claimsPrincipalAccessor)
        {
            _dbContext = dbContext;
            _schemaProvider = schemaProvider;
            _serviceProvider = serviceProvider;
            _claimsPrincipalAccessor = claimsPrincipalAccessor;
        }

        [FunctionName("GraphQL")]
        public async Task<object?> GraphQlEndpoint(
            [HttpTrigger(AuthorizationLevel.Anonymous, new[] { "post" }, Route = "graphql")]
            HttpRequestMessage req)
        {
            var principal = _claimsPrincipalAccessor?.GetClaimsPrincipal();
            if (principal == null || !principal.Claims.Any())
            {
                return req.CreateErrorResponse(HttpStatusCode.Unauthorized, "Not logged in");
            }

            var body = await req.Content!.ReadAsStringAsync();

            var jsonOptions = new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            jsonOptions.Converters.Add(new JsonStringEnumConverter());
            var query = JsonSerializer.Deserialize<QueryRequest>(body, jsonOptions);

            if (query is null)
            {
                return new BadRequestObjectResult("No query or invalid query syntax in request body");
            }

            var results = _schemaProvider.ExecuteRequestWithContext(query, _dbContext, _serviceProvider, principal, new ExecutionOptions() { });

            if (results != null && results.Errors != null && results.Errors.Any())
            {
                Console.Write(results.Errors.First().Message);
            }

            return results;
        }

        [FunctionName("GraphQLSchema")]
        public string GraphQLSchema([HttpTrigger(AuthorizationLevel.Anonymous, new[] { "get" }, Route = "graphql-schema")] HttpRequestMessage req)
        {
            return _schemaProvider.ToGraphQLSchemaString();
        }
    }
}