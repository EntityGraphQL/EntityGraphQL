---
sidebar_position: 3
---

# Async Fields

EntityGraphQL provides comprehensive support for asynchronous field resolution, allowing you to integrate with external services, databases, and APIs while maintaining control over performance and concurrency.

EntityGraphQL handles async execution by compiling all fields into expression trees first, then executing them with proper concurrency control and task coordination.

## Basic Async Field Setup

### Using ResolveAsync

The `ResolveAsync` method allows you to define fields that execute asynchronously using injected services:

```csharp
public class WeatherService
{
    private readonly HttpClient httpClient;

    public WeatherService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<WeatherData> GetWeatherAsync(string location)
    {
        var response = await httpClient.GetAsync($"/weather?location={location}");
        return await response.Content.ReadFromJsonAsync<WeatherData>();
    }
}

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Location { get; set; }
}

// Add async field to your schema
var schema = SchemaBuilder.FromObject<MyContext>();

schema.Type<Person>()
    .AddField("weather", "Current weather for this person's location")
    .ResolveAsync<WeatherService>((person, weatherService) =>
        weatherService.GetWeatherAsync(person.Location));
```

## Concurrency Control

EntityGraphQL provides three levels of concurrency control to help you manage resource usage and prevent overwhelming external services.

All concurrency limits apply only to the currently executing query.

### Field-Level Concurrency

Limit concurrency for individual fields:

```csharp
schema.Type<Person>()
    .AddField("expensiveOperation", "Resource-intensive operation")
    .ResolveAsync<ExpensiveService>((person, service) =>
        service.DoExpensiveWorkAsync(person.Id),
        maxConcurrency: 5); // Only 5 concurrent operations for this field
```

### Service-Level Concurrency

Configure concurrency limits for entire services across all fields that use them:

```csharp
var executionOptions = new ExecutionOptions
{
    ServiceConcurrencyLimits = new Dictionary<Type, int>
    {
        [typeof(WeatherService)] = 10,  // Max 10 concurrent weather calls
        [typeof(DatabaseService)] = 3,  // Max 3 concurrent database operations
        [typeof(EmailService)] = 2      // Max 2 concurrent email sends
    }
};

var result = await schema.ExecuteRequestAsync(query, context, serviceProvider, executionOptions);
```

### Query-Level Concurrency

Set a global limit for all async operations in a single query:

```csharp
var executionOptions = new ExecutionOptions
{
    MaxQueryConcurrency = 20  // Maximum 20 concurrent operations across entire query
};

var result = await schema.ExecuteRequestAsync(query, context, serviceProvider, executionOptions);
```

### Hierarchical Concurrency Control

EntityGraphQL applies concurrency limits hierarchically - each level respects the limits above it:

1. **Query Level**: Global limit for the entire query
2. **Service Level**: Limit per service type
3. **Field Level**: Limit per individual field

For example, with these settings:

- Query limit: 50
- WeatherService limit: 10
- Field limit: 3

The field will never exceed 3 concurrent operations, the WeatherService will never exceed 10 concurrent operations, and the entire query will never exceed 50 concurrent operations.

```csharp
var executionOptions = new ExecutionOptions
{
    MaxQueryConcurrency = 50,
    ServiceConcurrencyLimits = new Dictionary<Type, int>
    {
        [typeof(WeatherService)] = 10
    }
};

schema.Type<Person>()
    .AddField("weather", "Weather data")
    .ResolveAsync<WeatherService>((person, service) =>
        service.GetWeatherAsync(person.Location),
        maxConcurrency: 3);
```

### Implementation Overview

You can view the implementation in `ConcurrencyLimitFieldExtension` and `ConcurrencyLimiterRegistry`. It uses `SemaphoreSlim` to control the concurrency limits.

This controls when the async method _starts_. Taking the `GetWeatherAsync` example above, if we are resolving `weather` within a list of 100 people `GetWeatherAsync` will only be started/called 3 at a time.

If you have no limits set up (the default) all `async` fields will start at the same time.

## Cancellation Support

EntityGraphQL provides comprehensive support for `CancellationToken` to enable cooperative cancellation of long-running operations. This allows you to gracefully handle request timeouts, client disconnections, and manual cancellation.

### Basic CancellationToken Usage

#### In Service Methods

Your service methods can accept a `CancellationToken` parameter, which EntityGraphQL will automatically provide:

```csharp
public class WeatherService
{
    private readonly HttpClient httpClient;

    public WeatherService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<WeatherData> GetWeatherAsync(string location, CancellationToken cancellationToken = default)
    {
        // Pass cancellationToken to async operations
        var response = await httpClient.GetAsync($"/weather?location={location}", cancellationToken);
        return await response.Content.ReadFromJsonAsync<WeatherData>(cancellationToken: cancellationToken);
    }
}
```

#### In ResolveAsync Methods

Use the `ResolveAsync` overload that accepts a `CancellationToken` parameter:

```csharp
schema.Type<Person>()
    .AddField("weather", "Current weather for this person's location")
    .ResolveAsync<WeatherService, CancellationToken>((person, weatherService, cancellationToken) =>
        weatherService.GetWeatherAsync(person.Location, cancellationToken));
```

### ASP.NET Core Integration

When using EntityGraphQL with ASP.NET Core, the framework automatically uses `HttpContext.RequestAborted` as the cancellation token. This means operations are cancelled when:

- The client disconnects
- The request times out
- The server is shutting down

```csharp
// In your controller or minimal API
app.MapPost("/graphql", async (HttpContext context, GraphQLRequest request) =>
{
    var result = await schema.ExecuteRequestAsync(
        request.Query,
        myContext,
        context.RequestServices,
        cancellationToken: context.RequestAborted); // Automatically handled by EntityGraphQL.AspNet

    return result;
});
```

### Manual Cancellation

You can also provide your own `CancellationToken` for scenarios like:

- Custom timeout policies
- Manual cancellation based on business logic
- Testing scenarios

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30-second timeout

var result = await schema.ExecuteRequestAsync(
    query,
    context,
    serviceProvider,
    cancellationToken: cts.Token);
```

### Concurrency and Cancellation

Cancellation works seamlessly with EntityGraphQL's concurrency control features. When a cancellation is requested:

1. All pending async operations receive the cancellation signal
2. Operations waiting for semaphore slots are cancelled immediately
3. Currently executing operations can respond to cancellation cooperatively

```csharp
schema.Type<Person>()
    .AddField("expensiveOperation", "Resource-intensive operation")
    .ResolveAsync<ExpensiveService, CancellationToken>((person, service, cancellationToken) =>
        service.DoExpensiveWorkAsync(person.Id, cancellationToken),
        maxConcurrency: 5); // Concurrency limits + cancellation support
```
