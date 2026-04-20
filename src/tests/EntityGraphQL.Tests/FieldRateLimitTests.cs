using System;
using System.Collections.Generic;
using System.Security.Claims;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.QueryLimits;
using Xunit;
using TaskT = System.Threading.Tasks.Task;
using ValueTaskT = System.Threading.Tasks.ValueTask<EntityGraphQL.Schema.QueryLimits.IFieldRateLimitLease>;

namespace EntityGraphQL.Tests;

public class FieldRateLimitTests
{
    private static SchemaProvider<TestDataContext> BuildSchema() => SchemaBuilder.FromObject<TestDataContext>();

    [Fact]
    public async TaskT UntaggedField_NotAffected_EvenWhenServiceRegistered()
    {
        var schema = BuildSchema();
        var data = new TestDataContext();
        var limiter = new RecordingLimiter();

        var result = await schema.ExecuteRequestWithContextAsync(new QueryRequest { Query = "{ totalPeople }" }, data, null, null, new ExecutionOptions { FieldRateLimitService = limiter });
        Assert.Null(result.Errors);
        Assert.Empty(limiter.Requests);
    }

    [Fact]
    public async TaskT TaggedField_AcquiresPermit_ThenReleases()
    {
        var schema = BuildSchema();
        schema.Type<TestDataContext>().GetField("totalPeople", null).AddRateLimit("total-people");
        var data = new TestDataContext();
        var limiter = new RecordingLimiter();

        var result = await schema.ExecuteRequestWithContextAsync(new QueryRequest { Query = "{ totalPeople }" }, data, null, null, new ExecutionOptions { FieldRateLimitService = limiter });
        Assert.Null(result.Errors);
        Assert.Single(limiter.Requests);
        Assert.Equal("total-people", limiter.Requests[0].PolicyName);
        Assert.Null(limiter.Requests[0].UserKey);
        Assert.Equal(1, limiter.DisposeCount);
    }

    [Fact]
    public async TaskT TaggedField_DeniedAcquisition_AbortsBeforeExecute()
    {
        var schema = BuildSchema();
        schema.Type<TestDataContext>().GetField("totalPeople", null).AddRateLimit("total-people");

        var data = new TestDataContext();
        var limiter = new RecordingLimiter(allowAll: false);
        var beforeExecutingCalled = false;

        var result = await schema.ExecuteRequestWithContextAsync(
            new QueryRequest { Query = "{ totalPeople }" },
            data,
            null,
            null,
            new ExecutionOptions
            {
                FieldRateLimitService = limiter,
                BeforeExecuting = (expr, isFinal) =>
                {
                    beforeExecutingCalled = true;
                    return expr;
                },
            }
        );

        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors!, e => e.Message.Contains("Rate limit exceeded"));
        Assert.False(beforeExecutingCalled, "execution must not start when rate limit denies");
    }

    [Fact]
    public async TaskT UserSpecific_UsesUserKey()
    {
        var schema = BuildSchema();
        schema.Type<TestDataContext>().GetField("totalPeople", null).AddRateLimit("per-user", userSpecific: true);
        var data = new TestDataContext();
        var limiter = new RecordingLimiter();

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test"));
        await schema.ExecuteRequestWithContextAsync(new QueryRequest { Query = "{ totalPeople }" }, data, null, user, new ExecutionOptions { FieldRateLimitService = limiter });
        Assert.Equal("alice", limiter.Requests[0].UserKey);
    }

    [Fact]
    public async TaskT UserSpecific_CustomSelector_IsHonored()
    {
        var schema = BuildSchema();
        schema.Type<TestDataContext>().GetField("totalPeople", null).AddRateLimit("per-user", userSpecific: true);
        var data = new TestDataContext();
        var limiter = new RecordingLimiter();

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("api-key", "deadbeef")], "test"));
        await schema.ExecuteRequestWithContextAsync(
            new QueryRequest { Query = "{ totalPeople }" },
            data,
            null,
            user,
            new ExecutionOptions { FieldRateLimitService = limiter, UserKeySelector = p => p?.FindFirst("api-key")?.Value }
        );
        Assert.Equal("deadbeef", limiter.Requests[0].UserKey);
    }

    [Fact]
    public async TaskT DuplicateSelections_CountedPerSelection()
    {
        var schema = BuildSchema();
        schema.Type<TestDataContext>().GetField("totalPeople", null).AddRateLimit("total");
        var data = new TestDataContext();
        var limiter = new RecordingLimiter();

        // aliased four times — one AcquireAsync call with permitCount=4 (same (policy, userKey) tuple,
        // count incremented for each selection).
        await schema.ExecuteRequestWithContextAsync(
            new QueryRequest { Query = "{ a: totalPeople b: totalPeople c: totalPeople d: totalPeople }" },
            data,
            null,
            null,
            new ExecutionOptions { FieldRateLimitService = limiter }
        );
        Assert.Single(limiter.Requests);
        Assert.Equal(4, limiter.Requests[0].PermitCount);
    }

    [Fact]
    public async TaskT FragmentSpread_ContributesToAcquisition()
    {
        var schema = BuildSchema();
        schema.Type<TestDataContext>().GetField("totalPeople", null).AddRateLimit("total");
        var data = new TestDataContext();
        var limiter = new RecordingLimiter();

        var query =
            @"
            query {
                ...Counts
            }
            fragment Counts on Query {
                totalPeople
            }";

        await schema.ExecuteRequestWithContextAsync(new QueryRequest { Query = query }, data, null, null, new ExecutionOptions { FieldRateLimitService = limiter });
        Assert.Single(limiter.Requests);
        Assert.Equal("total", limiter.Requests[0].PolicyName);
    }

    [Fact]
    public async TaskT MultiplePolicies_AllAcquired()
    {
        var schema = BuildSchema();
        var f = schema.Type<TestDataContext>().GetField("totalPeople", null);
        f.AddRateLimit("global");
        f.AddRateLimit("per-user", userSpecific: true);
        var data = new TestDataContext();
        var limiter = new RecordingLimiter();

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "bob")], "test"));
        await schema.ExecuteRequestWithContextAsync(new QueryRequest { Query = "{ totalPeople }" }, data, null, user, new ExecutionOptions { FieldRateLimitService = limiter });
        Assert.Equal(2, limiter.Requests.Count);
        Assert.Contains(limiter.Requests, r => r.PolicyName == "global" && r.UserKey == null);
        Assert.Contains(limiter.Requests, r => r.PolicyName == "per-user" && r.UserKey == "bob");
    }

    [Fact]
    public async TaskT DenialDisposesAlreadyAcquiredLeases()
    {
        var schema = BuildSchema();
        var f = schema.Type<TestDataContext>().GetField("totalPeople", null);
        f.AddRateLimit("policy-a");
        f.AddRateLimit("policy-b");
        var data = new TestDataContext();
        // policy-a is allowed, policy-b is denied. Policy-a's lease must still be disposed.
        var limiter = new RecordingLimiter(policy => policy != "policy-b");

        var result = await schema.ExecuteRequestWithContextAsync(new QueryRequest { Query = "{ totalPeople }" }, data, null, null, new ExecutionOptions { FieldRateLimitService = limiter });
        Assert.NotNull(result.Errors);
        // both leases created (one acquired, one denied) should be disposed
        Assert.Equal(limiter.Requests.Count, limiter.DisposeCount);
    }

    [Fact]
    public async TaskT LeaseHeldUntilAfterExecute()
    {
        var schema = BuildSchema();
        schema.Type<TestDataContext>().GetField("totalPeople", null).AddRateLimit("hold");
        var data = new TestDataContext();
        var limiter = new RecordingLimiter();

        var disposeBeforeExecute = false;
        var options = new ExecutionOptions
        {
            FieldRateLimitService = limiter,
            BeforeExecuting = (expr, isFinal) =>
            {
                // if the lease had already been disposed, this would be non-zero before execute finishes
                disposeBeforeExecute = limiter.DisposeCount > 0;
                return expr;
            },
        };
        await schema.ExecuteRequestWithContextAsync(new QueryRequest { Query = "{ totalPeople }" }, data, null, null, options);
        Assert.False(disposeBeforeExecute);
        Assert.Equal(1, limiter.DisposeCount);
    }

    private sealed class RecordingLimiter : IFieldRateLimitService
    {
        private readonly Func<string, bool> allow;

        public RecordingLimiter()
            : this(_ => true) { }

        public RecordingLimiter(bool allowAll)
            : this(_ => allowAll) { }

        public RecordingLimiter(Func<string, bool> allow)
        {
            this.allow = allow;
        }

        public List<FieldRateLimitRequest> Requests { get; } = [];
        public int DisposeCount { get; private set; }

        public ValueTaskT TryAcquireAsync(FieldRateLimitRequest request)
        {
            Requests.Add(request);
            var acquired = allow(request.PolicyName);
            IFieldRateLimitLease lease = new Lease(acquired, () => DisposeCount++);
            return new ValueTaskT(lease);
        }

        private sealed class Lease : IFieldRateLimitLease
        {
            private readonly Action onDispose;
            private bool disposed;

            public Lease(bool acquired, Action onDispose)
            {
                IsAcquired = acquired;
                this.onDispose = onDispose;
            }

            public bool IsAcquired { get; }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                onDispose();
            }
        }
    }
}
