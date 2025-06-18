using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    public async Task GraphQL_Endpoint_FollowSpec_Supports_ApplicationWildcard()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/*"));
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

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Supports_Not_First()
    {
        var graphqlRequest = new { query = "{ hello }" };
        var requestBody = new StringContent(System.Text.Json.JsonSerializer.Serialize(graphqlRequest), Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = requestBody };
        request.Headers.Add("Accept", "text/event-stream, application/json, application/graphql-response+json, ");
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

/// <summary>
/// https://github.com/EntityGraphQL/EntityGraphQL/issues/443
/// Since it's not possible to force HttpClient to not parse the headers, and we can't use it to send raw HTTP,
/// Use a TCP connection to reproduce the issue in tests (it requires an ugly creation of the server, by copying the Program.cs code,
/// or moving most of this code to a shared class that can be used by both Program.cs and the test)
/// </summary>
public class CustomWebApplicationFactory<TEntry> : WebApplicationFactory<TEntry>
    where TEntry : class
{
    // The second, real Kestrel-based minimal API app for raw TCP testing.
    public WebApplication? RealApp { get; private set; }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Let the base class spin up the standard in-memory TestServer for WebApplicationFactory
        IHost testHost = base.CreateHost(builder);

        // Build an *identical* minimal API app (duplicating Program.cs setup) so we can run it on real Kestrel
        WebApplicationBuilder realBuilder = WebApplication.CreateBuilder([]);

        // Match the same services from Program.cs
        realBuilder.Services.AddGraphQLValidator();
        realBuilder.Services.AddGraphQLSchema<TestQueryType>(configure =>
        {
            configure.ConfigureSchema = schema =>
            {
                schema
                    .Mutation()
                    .Add(
                        "mutationFail",
                        (TestQueryType db, IGraphQLValidator validator) =>
                        {
                            validator.AddError("This is a test error");
                            if (validator.HasErrors)
                            {
                                return null;
                            }

                            return db.Hello;
                        }
                    )
                    .IsNullable(false);
            };
        });
        realBuilder.Services.AddScoped<TestQueryType>();

        // Build the real Kestrel app
        WebApplication realApp = realBuilder.Build();

        // Same mapping as Program.cs
        realApp.MapGraphQL<TestQueryType>(followSpec: true);

        // Force ephemeral port
        realApp.Urls.Add("http://127.0.0.1:0");

        // Start the real Kestrel server
        realApp.Start();
        RealApp = realApp;

        // Return the "test host" so WebApplicationFactory is happy
        return testHost;
    }
}

public class RawTcpTest : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public RawTcpTest(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        // This ensures the base test host is fully created. It doesn't work without this line.
        // This forces the base class to spin up its TestServer host
        // which triggers our overridden CreateHost().
        _ = _factory.CreateClient();
    }

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Supports_Multiple_SingleLine_NotParsedAcceptHeader()
    {
        // The second, real Kestrel-based WebApplication
        WebApplication realApp = _factory.RealApp ?? throw new InvalidOperationException("RealApp is null.");

        // Get the real ephemeral port
        IServer server = realApp.Services.GetRequiredService<IServer>();
        IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()!;
        string address = addresses.Addresses.First(); // e.g. http://127.0.0.1:12345
        Uri uri = new(address);

        // Connect via TCP
        using TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(uri.Host, uri.Port);

        await using NetworkStream networkStream = tcpClient.GetStream();

        // Raw HTTP request
        // The body is exactly 21 bytes long: {"query":"{ hello }"}
        const string rawHttp =
            "POST /graphql HTTP/1.1\r\n"
            + "Host: localhost\r\n"
            + "Accept: application/graphql-response+json, application/json, text/event-stream\r\n"
            + "Content-Type: application/json\r\n"
            + "Content-Length: 21\r\n"
            + "\r\n"
            + "{\"query\":\"{ hello }\"}";

        byte[] requestBytes = Encoding.UTF8.GetBytes(rawHttp);
        await networkStream.WriteAsync(requestBytes);

        // Read the raw HTTP response
        byte[] buffer = new byte[4096];
        int bytesRead = await networkStream.ReadAsync(buffer);
        string responseText = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        // Assert
        Assert.Contains("200 OK", responseText);
        Assert.Contains("\"hello\":\"world\"", responseText);
        Assert.True(
            responseText.Contains("Content-Type: application/json\r\n", StringComparison.OrdinalIgnoreCase)
                || responseText.Contains("Content-Type: application/graphql-response+json\r\n", StringComparison.OrdinalIgnoreCase)
                || responseText.Contains("Content-Type: text/event-stream\r\n", StringComparison.OrdinalIgnoreCase),
            "Response text did not contain an expected Content-Type header. Received: " + responseText
        );
    }

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Supports_SingleAppJsonAcceptMediaType_NotParsedAcceptHeader()
    {
        // The second, real Kestrel-based WebApplication
        WebApplication realApp = _factory.RealApp ?? throw new InvalidOperationException("RealApp is null.");

        // Get the real ephemeral port
        IServer server = realApp.Services.GetRequiredService<IServer>();
        IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()!;
        string address = addresses.Addresses.First(); // e.g. http://127.0.0.1:12345
        Uri uri = new(address);

        // Connect via TCP
        using TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(uri.Host, uri.Port);

        await using NetworkStream networkStream = tcpClient.GetStream();

        // Raw HTTP request
        // The body is exactly 21 bytes long: {"query":"{ hello }"}
        const string rawHttp =
            "POST /graphql HTTP/1.1\r\n"
            + "Host: localhost\r\n"
            + "Accept: application/json\r\n"
            + "Content-Type: application/json\r\n"
            + "Content-Length: 21\r\n"
            + "\r\n"
            + "{\"query\":\"{ hello }\"}";

        byte[] requestBytes = Encoding.UTF8.GetBytes(rawHttp);
        await networkStream.WriteAsync(requestBytes);

        // Read the raw HTTP response
        byte[] buffer = new byte[4096];
        int bytesRead = await networkStream.ReadAsync(buffer);
        string responseText = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        // Assert
        Assert.Contains("200 OK", responseText);
        Assert.Contains("\"hello\":\"world\"", responseText);
        Assert.True(
            responseText.Contains("Content-Type: application/json\r\n", StringComparison.OrdinalIgnoreCase)
                || responseText.Contains("Content-Type: application/graphql-response+json\r\n", StringComparison.OrdinalIgnoreCase)
                || responseText.Contains("Content-Type: text/event-stream\r\n", StringComparison.OrdinalIgnoreCase),
            "Response text did not contain an expected Content-Type header. Received: " + responseText
        );
    }

    [Fact]
    public async Task GraphQL_Endpoint_FollowSpec_Supports_Header_Not_First()
    {
        // The second, real Kestrel-based WebApplication
        WebApplication realApp = _factory.RealApp ?? throw new InvalidOperationException("RealApp is null.");

        // Get the real ephemeral port
        IServer server = realApp.Services.GetRequiredService<IServer>();
        IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()!;
        string address = addresses.Addresses.First(); // e.g. http://127.0.0.1:12345
        Uri uri = new(address);

        // Connect via TCP
        using TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(uri.Host, uri.Port);

        await using NetworkStream networkStream = tcpClient.GetStream();

        // Raw HTTP request
        // The body is exactly 21 bytes long: {"query":"{ hello }"}
        const string rawHttp =
            "POST /graphql HTTP/1.1\r\n"
            + "Host: localhost\r\n"
            + "Accept: text/event-stream, application/graphql-response+json, application/json\r\n"
            + "Content-Type: application/json\r\n"
            + "Content-Length: 21\r\n"
            + "\r\n"
            + "{\"query\":\"{ hello }\"}";

        byte[] requestBytes = Encoding.UTF8.GetBytes(rawHttp);
        await networkStream.WriteAsync(requestBytes);

        // Read the raw HTTP response
        byte[] buffer = new byte[4096];
        int bytesRead = await networkStream.ReadAsync(buffer);
        string responseText = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        // Assert
        Assert.Contains("200 OK", responseText);
        Assert.Contains("\"hello\":\"world\"", responseText);
        Assert.True(
            responseText.Contains("Content-Type: application/json\r\n", StringComparison.OrdinalIgnoreCase)
                || responseText.Contains("Content-Type: application/graphql-response+json\r\n", StringComparison.OrdinalIgnoreCase)
                || responseText.Contains("Content-Type: text/event-stream\r\n", StringComparison.OrdinalIgnoreCase),
            "Response text did not contain an expected Content-Type header. Received: " + responseText
        );
    }

    [Fact]
    public async Task GraphQL_Endpoint_200_On_Chunked_Data_Query()
    {
        // The second, real Kestrel-based WebApplication
        WebApplication realApp = _factory.RealApp ?? throw new InvalidOperationException("RealApp is null.");

        // Get the real ephemeral port
        IServer server = realApp.Services.GetRequiredService<IServer>();
        IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()!;
        string address = addresses.Addresses.First(); // e.g. http://127.0.0.1:12345
        Uri uri = new(address);

        HttpClient client = new();
        client.BaseAddress = new Uri($"http://{uri.Host}:{uri.Port}");
        client.DefaultRequestHeaders.Add("Accept", "*/*");
        // PostAsJsonAsync adds the following header:
        // Transfer-Encoding = chunked
        // but ContentLength isn't forwarded, hence it's null or zero
        // if PostAsJsonAsync is used with WebApplicationFactory,
        // i.e. the in-memory test server, with no network involved,
        // it works fine and the ContentLength is set correctly.
		// hence, the CustomWebApplicationFactory is used to ensure
		// network communication is used, and the ContentLength is not set.
        HttpResponseMessage resp = await client.PostAsJsonAsync("/graphql", new { query = "{ hello }" });
        resp.EnsureSuccessStatusCode();

        string json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"hello\":\"world\"", json);
    }
}