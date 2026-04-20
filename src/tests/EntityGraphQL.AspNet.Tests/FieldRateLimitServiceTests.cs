using System.Security.Claims;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.QueryLimits;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.AspNet.Tests;

public class FieldRateLimitServiceTests
{
    private static SchemaProvider<RateLimitContext> BuildSchema() => SchemaBuilder.FromObject<RateLimitContext>();

    [Fact]
    public async Task AddGraphQLFieldRateLimit_RegistersServiceInDI()
    {
        var services = new ServiceCollection();
        services.AddGraphQLFieldRateLimit(opts => opts.AddFixedWindowPolicy("p1", permitLimit: 10, window: TimeSpan.FromMinutes(1)));

        await using var sp = services.BuildServiceProvider();
        var svc = sp.GetService<IFieldRateLimitService>();
        Assert.NotNull(svc);
        Assert.IsType<DefaultFieldRateLimitService>(svc);
    }

    [Fact]
    public async Task FixedWindow_DeniesAfterLimitExceeded()
    {
        var schema = BuildSchema();
        schema.Type<RateLimitContext>().GetField("value", null).AddRateLimit("fixed");

        var services = new ServiceCollection();
        services.AddGraphQLFieldRateLimit(opts => opts.AddFixedWindowPolicy("fixed", permitLimit: 2, window: TimeSpan.FromMinutes(1)));
        await using var sp = services.BuildServiceProvider();
        var limitSvc = sp.GetRequiredService<IFieldRateLimitService>();

        var gql = new QueryRequest { Query = "{ value }" };
        var data = new RateLimitContext();
        var opt = new ExecutionOptions { FieldRateLimitService = limitSvc };

        Assert.Null((await schema.ExecuteRequestWithContextAsync(gql, data, null, null, opt)).Errors);
        Assert.Null((await schema.ExecuteRequestWithContextAsync(gql, data, null, null, opt)).Errors);

        // third one exceeds the window
        var denied = await schema.ExecuteRequestWithContextAsync(gql, data, null, null, opt);
        Assert.NotNull(denied.Errors);
        Assert.Contains(denied.Errors!, e => e.Message.Contains("Rate limit exceeded"));
    }

    [Fact]
    public async Task PerSelection_AliasBatchingDrainsBucketInOneRequest()
    {
        var schema = BuildSchema();
        schema.Type<RateLimitContext>().GetField("value", null).AddRateLimit("persel");

        var services = new ServiceCollection();
        services.AddGraphQLFieldRateLimit(opts => opts.AddFixedWindowPolicy("persel", permitLimit: 5, window: TimeSpan.FromMinutes(1)));
        await using var sp = services.BuildServiceProvider();
        var limitSvc = sp.GetRequiredService<IFieldRateLimitService>();

        var data = new RateLimitContext();
        var opt = new ExecutionOptions { FieldRateLimitService = limitSvc };

        // 6 aliases, bucket is 5 → denied on the first request
        var aliased = new QueryRequest { Query = "{ a: value b: value c: value d: value e: value f: value }" };
        var denied = await schema.ExecuteRequestWithContextAsync(aliased, data, null, null, opt);
        Assert.NotNull(denied.Errors);
        Assert.Contains(denied.Errors!, e => e.Message.Contains("Rate limit exceeded"));
    }

    [Fact]
    public async Task OncePerRequest_DedupesAliasesToSinglePermit()
    {
        var schema = BuildSchema();
        schema.Type<RateLimitContext>().GetField("value", null).AddRateLimit("login");

        var services = new ServiceCollection();
        services.AddGraphQLFieldRateLimit(opts => opts.AddFixedWindowPolicy("login", permitLimit: 3, window: TimeSpan.FromMinutes(1), oncePerRequest: true));
        await using var sp = services.BuildServiceProvider();
        var limitSvc = sp.GetRequiredService<IFieldRateLimitService>();

        var data = new RateLimitContext();
        var opt = new ExecutionOptions { FieldRateLimitService = limitSvc };

        // Each request aliases the field 5 times but only charges 1 permit. 3 requests should succeed.
        var aliased = new QueryRequest { Query = "{ a: value b: value c: value d: value e: value }" };
        Assert.Null((await schema.ExecuteRequestWithContextAsync(aliased, data, null, null, opt)).Errors);
        Assert.Null((await schema.ExecuteRequestWithContextAsync(aliased, data, null, null, opt)).Errors);
        Assert.Null((await schema.ExecuteRequestWithContextAsync(aliased, data, null, null, opt)).Errors);

        var denied = await schema.ExecuteRequestWithContextAsync(aliased, data, null, null, opt);
        Assert.NotNull(denied.Errors);
    }

    [Fact]
    public async Task UserSpecific_PartitionsSeparately()
    {
        var schema = BuildSchema();
        schema.Type<RateLimitContext>().GetField("value", null).AddRateLimit("per-user", userSpecific: true);

        var services = new ServiceCollection();
        services.AddGraphQLFieldRateLimit(opts => opts.AddFixedWindowPolicy("per-user", permitLimit: 1, window: TimeSpan.FromMinutes(1)));
        await using var sp = services.BuildServiceProvider();
        var limitSvc = sp.GetRequiredService<IFieldRateLimitService>();

        var gql = new QueryRequest { Query = "{ value }" };
        var data = new RateLimitContext();
        var opt = new ExecutionOptions { FieldRateLimitService = limitSvc };

        var alice = Principal("alice");
        var bob = Principal("bob");

        Assert.Null((await schema.ExecuteRequestWithContextAsync(gql, data, null, alice, opt)).Errors);
        // alice is at her limit
        var aliceDenied = await schema.ExecuteRequestWithContextAsync(gql, data, null, alice, opt);
        Assert.NotNull(aliceDenied.Errors);
        // bob has his own partition and should still be allowed
        Assert.Null((await schema.ExecuteRequestWithContextAsync(gql, data, null, bob, opt)).Errors);
    }

    [Fact]
    public async Task UnknownPolicy_ThrowsInvalidOperation()
    {
        var schema = BuildSchema();
        schema.Type<RateLimitContext>().GetField("value", null).AddRateLimit("never-registered");

        var services = new ServiceCollection();
        services.AddGraphQLFieldRateLimit(opts => opts.AddFixedWindowPolicy("some-other-policy", permitLimit: 1, window: TimeSpan.FromMinutes(1)));
        await using var sp = services.BuildServiceProvider();
        var limitSvc = sp.GetRequiredService<IFieldRateLimitService>();

        var gql = new QueryRequest { Query = "{ value }" };
        var data = new RateLimitContext();
        var opt = new ExecutionOptions { FieldRateLimitService = limitSvc };

        var result = await schema.ExecuteRequestWithContextAsync(gql, data, null, null, opt);
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors!, e => e.Message.Contains("never-registered"));
    }

    [Fact]
    public async Task Concurrency_HoldsPermit_UntilExecutionCompletes()
    {
        var schema = BuildSchema();
        schema.Type<RateLimitContext>().GetField("value", null).AddRateLimit("concurrent");

        var services = new ServiceCollection();
        services.AddGraphQLFieldRateLimit(opts => opts.AddConcurrencyPolicy("concurrent", permitLimit: 1));
        await using var sp = services.BuildServiceProvider();
        var limitSvc = sp.GetRequiredService<IFieldRateLimitService>();

        var gql = new QueryRequest { Query = "{ value }" };
        var data = new RateLimitContext();
        var inExecute = new TaskCompletionSource<bool>();
        var releaseExecute = new TaskCompletionSource<bool>();

        var slowOpt = new ExecutionOptions
        {
            FieldRateLimitService = limitSvc,
            // Block inside execution to simulate a long-running resolver
            BeforeExecuting = (expr, isFinal) =>
            {
                inExecute.TrySetResult(true);
                releaseExecute.Task.GetAwaiter().GetResult();
                return expr;
            },
        };
        var fastOpt = new ExecutionOptions { FieldRateLimitService = limitSvc };

        var slow = Task.Run(() => schema.ExecuteRequestWithContextAsync(gql, data, null, null, slowOpt));
        await inExecute.Task; // slow request has acquired the one permit and is inside execute

        // try a concurrent request — queue limit is 0 so it should be denied immediately
        var concurrent = await schema.ExecuteRequestWithContextAsync(gql, data, null, null, fastOpt);
        Assert.NotNull(concurrent.Errors);
        Assert.Contains(concurrent.Errors!, e => e.Message.Contains("Rate limit exceeded"));

        releaseExecute.TrySetResult(true);
        await slow;

        // with the slow one done and lease released, next request succeeds
        var after = await schema.ExecuteRequestWithContextAsync(gql, data, null, null, fastOpt);
        Assert.Null(after.Errors);
    }

    [Fact]
    public async Task ConsumerCanReplaceImplementation()
    {
        // Custom implementation registered before AddGraphQLFieldRateLimit — TryAddSingleton must not replace it.
        var services = new ServiceCollection();
        services.AddSingleton<IFieldRateLimitService, SentinelService>();
        services.AddGraphQLFieldRateLimit(opts => opts.AddFixedWindowPolicy("x", 1, TimeSpan.FromSeconds(1)));

        await using var sp = services.BuildServiceProvider();
        var svc = sp.GetRequiredService<IFieldRateLimitService>();
        Assert.IsType<SentinelService>(svc);
    }

    private static ClaimsPrincipal Principal(string name)
    {
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "test"));
    }

    private sealed class SentinelService : IFieldRateLimitService
    {
        public ValueTask<IFieldRateLimitLease> TryAcquireAsync(FieldRateLimitRequest request) => throw new NotImplementedException();
    }

    public class RateLimitContext
    {
        public int Value => 42;
    }
}
