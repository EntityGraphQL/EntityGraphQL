using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace EntityGraphQL.AspNet.Tests;

/// <summary>
/// Testing we follow https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md
/// </summary>
public class EntityGraphQLEndpointRouteExtensionsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public EntityGraphQLEndpointRouteExtensionsTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQL_Endpoint_No_Accept_Header_Ok()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/graphql", requestBody);

        // will default to JSON
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GraphQL_Endpoint_Returns_Correct_ResponseType_Json()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/json", "Content-Type is not application/json.");

        await CheckResponseIsValid(response);
    }

    [Fact]
    public async Task GraphQL_Endpoint_Returns_Correct_ResponseType_Graphql()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/graphql-response+json", "Content-Type is not application/graphql-response+json.");

        await CheckResponseIsValid(response);
    }

    [Fact]
    public async Task GraphQL_Endpoint_Returns_Correct_ResponseType_Error()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("something/else"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    }

    [Fact]
    public async Task GraphQL_Endpoint_Returns_Ok_With_Error_For_Invalid_Query()
    {
        var graphqlRequest = new { query = "query { nonExistentField }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Content.Headers.ContentType?.MediaType == "application/graphql-response+json" || response.Content.Headers.ContentType?.MediaType == "application/json",
            "Content-Type is not application/graphql-response+json or application/json."
        );
        var responseString = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(responseString);

        Assert.NotNull(json);
        Assert.False(json.AsObject().ContainsKey("data"), "Expected 'data' field to be absent, but it exists in the JSON response.");
        Assert.NotNull(json["errors"]);
        Assert.True(json["errors"]!.AsArray().Count > 0);
    }

    [Fact]
    public async Task GraphQL_Endpoint_Fails_Without_ContentType()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        requestBody.Headers.ContentType = null;
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task GraphQL_Endpoint_400_On_Invalid_Json_Request()
    {
        var requestBody = new StringContent(@"{ query: not valid json }", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GraphQL_Endpoint_400_On_No_body()
    {
        var requestBody = new StringContent(@"", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GraphQL_Endpoint_400_On_Invalid_Json_Fields()
    {
        var requestBody = new StringContent(@"{""qeury"": ""{ hello }""}", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GraphQL_Endpoint_200_On_Valid_Request_Invalid_GraphQL()
    {
        var graphqlRequest = new { query = "{" }; // invalid graphql
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(responseString);

        Assert.NotNull(json);
        Assert.False(json.AsObject().ContainsKey("data"), "Expected 'data' field to be absent, but it exists in the JSON response.");
        Assert.NotNull(json["errors"]);
        Assert.True(json["errors"]!.AsArray().Count > 0);
    }

    [Fact]
    public async Task GraphQL_Endpoint_200_No_Data_Mutation_Error()
    {
        var graphqlRequest = new { query = "mutation { mutationFail }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(responseString);

        Assert.NotNull(json);
        Assert.False(json.AsObject().ContainsKey("data"), "Expected 'data' field to be absent, but it exists in the JSON response.");
        Assert.NotNull(json["errors"]);
        Assert.Equal("This is a test error", json["errors"]!.AsArray()[0]!["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Supports_Wildcard()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/graphql-response+json", "Content-Type is not application/graphql-response+json.");

        await CheckResponseIsValid(response);
    }

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Takes_Order()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/graphql-response+json", "Content-Type is not application/graphql-response+json.");

        await CheckResponseIsValid(response);
    }

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Takes_Order2()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/graphql-response+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/json", "Content-Type is not application/json.");

        await CheckResponseIsValid(response);
    }

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Supports_Multiple_SingleLine()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Add("Accept", "application/graphql-response+json, application/json, text/event-stream");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/graphql-response+json", "Content-Type is not application/graphql-response+json.");

        await CheckResponseIsValid(response);
    }

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Supports_Multiple_SingleLine_Order()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Add("Accept", "application/json, application/graphql-response+json, text/event-stream");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentType?.MediaType == "application/json", "Content-Type is not application/json.");

        await CheckResponseIsValid(response);
    }

    private static async Task CheckResponseIsValid(HttpResponseMessage response)
    {
        var responseString = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(responseString);

        Assert.NotNull(json);
        Assert.NotNull(json["data"]);
        Assert.NotNull(json["data"]!["hello"]);
        Assert.Equal("world", json["data"]!["hello"]!.ToString());
    }
}
