---
sidebar_position: 2
---

# Caching

Several ways of caching data are available in an asp.net project. Some will be described here.

## Output caching

[Output caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output), available in asp.net core, can be used to cache GraphQL requests/responses.

Register required services (`builder.Services.AddXyz()`) and set up the request pipeline (`app.UseXyz()`) appropriately.

Typical asp.net `Program.cs`:

```cs
var builder = WebApplication.CreateBuilder(args);

[...]

// highlight-next-line
builder.Services.AddOutputCache();

[...]

var app = builder.Build();

[...]

app.UseOutputCache();

[...]

app.MapGraphQL<MyDbContext>(
    configureEndpoint: endpointConventionBuilder =>
        // highlight-start
        endpointConventionBuilder.CacheOutput(outputCachePolicyBuilder =>
        {
            // Support HTTP POST requests, not supported by default
            outputCachePolicyBuilder.AddPolicy<GraphQLPolicy>();
            outputCachePolicyBuilder.VaryByValue(httpContext =>
            {
                httpContext.Request.EnableBuffering();
                var initialBodyStreamPosition = httpContext.Request.Body.Position;

                httpContext.Request.Body.Position = 0;

                using var bodyReader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
                var bodyContent = bodyReader.ReadToEndAsync()
                                            .Result
                                            .Replace(" ", "");

                httpContext.Request.Body.Position = initialBodyStreamPosition;

                return new KeyValuePair<string, string>("requestBody", bodyContent);
            });
            outputCachePolicyBuilder.Expire(TimeSpan.FromSeconds(MedicalDataApiConstants.OutputCache.DefaultExpireSeconds));
        }
        // highlight-end
    ));
```

- The code contained in the `VaryByValue` function will be called by the asp.net pipeline, from the `OutputCacheMiddleware` for every GraphQL request. If the same key, as the one returned by the function, is being found in the cache, its cached value will be returned. If it is not found, EntityGraphQL will execute the DB call and the returned result will be added to the cache.
- Adapt the `VaryByValue` function to your needs. E.g. if the GraphQL query depends on the accept language, it can be added as part of the key.
- With `builder.Services.AddOutputCache()`, in memory cache will be used. It can also be configured to use other backends e.g. [Redis](https://www.nuget.org/packages/Microsoft.Extensions.Caching.StackExchangeRedis).

We have to define and register our own `IOutputCachePolicy`, because the [DefaultPolicy](https://github.com/dotnet/dotnet/blob/main/src/aspnetcore/src/Middleware/OutputCaching/src/Policies/DefaultPolicy.cs#L70) only supports caching for HTTP GET endpoints, whereas GraphQL uses HTTP POST. Note that [all implementations](https://github.com/dotnet/dotnet/tree/main/src/aspnetcore/src/Middleware/OutputCaching/src/Policies) of `IOutputCachePolicy` are defined as `internal sealed` and thus, can't be extended.<br/>
The below `GraphQLPolicy` is a copy/paste of [DefaultPolicy](https://github.com/dotnet/dotnet/blob/main/src/aspnetcore/src/Middleware/OutputCaching/src/Policies/DefaultPolicy.cs), where only HTTP POST requests are supported, instead of GET.

```cs
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace MyApi.OutputCachePolicies;

internal sealed class GraphQLPolicy : IOutputCachePolicy
{
    /// <inheritdoc />
    ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var attemptOutputCaching = AttemptOutputCaching(context);
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = attemptOutputCaching;
        context.AllowCacheStorage = attemptOutputCaching;
        context.AllowLocking = true;

        // Vary by any query by default
        context.CacheVaryByRules.QueryKeys = "*";

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;

        // Verify existence of cookie headers
        if (!StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        // Check response code
        if (response.StatusCode != StatusCodes.Status200OK)
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        return ValueTask.CompletedTask;
    }

    private static bool AttemptOutputCaching(OutputCacheContext context)
    {
        // Check if the current request fulfills the requirements to be cached

        var request = context.HttpContext.Request;

        // Verify the method
        // highlight-start
        // Only allow POST requests to be cached (only change from DefaultPolicy)
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }
        // highlight-end

        // Verify existence of authorization headers
        if (!StringValues.IsNullOrEmpty(request.Headers.Authorization) || request.HttpContext.User?.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        return true;
    }
}
```
