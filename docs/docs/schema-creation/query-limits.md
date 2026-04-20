---
sidebar_position: 12
---

# Query limits (DoS protection)

EntityGraphQL ships a set of opt-in pre-execution guards that reject hostile or runaway GraphQL queries before any resolver runs. They are configured via `ExecutionOptions` and all default to "unlimited" — you must explicitly set them.

These guards protect against common GraphQL DoS vectors: deeply nested queries, batched-alias attacks (`{ a: field b: field c: field ... }`), fragment-spread amplification, and oversized list fetches via `first`/`take` arguments.

## Options

| Option | Purpose | Default |
|---|---|---|
| `MaxQueryDepth` | Hard cap on nesting depth. Fragment spreads and inline fragments do **not** add to depth. | `null` (unlimited) |
| `MaxQueryNodes` | Hard cap on total selected fields after fragment expansion. Defeats alias/spread amplification. | `null` (unlimited) |
| `MaxFieldAliases` | Hard cap on aliased selections (where response name differs from the schema field name). | `null` (unlimited) |
| `MaxQueryComplexity` | Hard cap on total cost computed by `IQueryComplexityAnalyzer`. | `null` (unlimited) |
| `QueryComplexityAnalyzer` | Override the default analyzer with your own. | `DefaultQueryComplexityAnalyzer` |

A limit of `null` or `0` means the check is skipped. When any guard fires, the request fails with a GraphQL `DocumentError` and nothing executes.

## Recommended production starting point

```cs
var options = new ExecutionOptions
{
    MaxQueryDepth = 10,
    MaxQueryNodes = 500,
    MaxFieldAliases = 30,
    MaxQueryComplexity = 1000,
};

schema.ExecuteRequest(gql, serviceProvider, user, options);
```

Tune the numbers against your real traffic. Start permissive and tighten if you see abuse; logs on the `DocumentError` tell you which limit fired.

## Depth

Depth counts how many levels of selection sets you traverse from the root. Fragments don't add a level — their contents are evaluated at the depth of the spread.

```graphql
{                          # depth 0
  projects {               # depth 1
    tasks {                # depth 2
      assignee {           # depth 3
        manager { id }     # depth 4 (5 if id counted)
      }
    }
  }
}
```

## Node count

`MaxQueryNodes` counts every field selection after fragment / inline-fragment expansion. This is the backstop for the alias and fragment-spread batching attacks — even if each individual field passes auth and schema checks, a query that selects 100,000 fields is rejected here.

## Alias count

An alias is any selection where the response name differs from the schema field name (`{ a: totalPeople }`). `MaxFieldAliases` lets you reject batched-alias attacks with a tighter limit than `MaxQueryNodes` since most legitimate queries need very few aliases.

## Complexity

The complexity analyzer assigns each field a cost (default `1`) and sums children. Fragments pass through — the spread itself contributes nothing, its contents do.

```
cost(field)      = baseCost + sum(cost(child))    # unless SetComplexity overrides
cost(fragment)   = sum(cost(child))
```

There is no built-in list-size multiplier. If a field's cost depends on arguments (say, `first: 100` makes it 100× more expensive), use the calculator form of `SetComplexity` described below — that avoids the library guessing at argument-name conventions and gives you full control.

### Fixed per-field cost

Expensive resolvers can be given an explicit cost — fluently or via an attribute.

**Fluent** — call `.SetComplexity(n)` after adding or updating a field:

```cs
using EntityGraphQL.Schema.QueryLimits;

schema.UpdateType<Project>(type =>
{
    type.GetField("tasks", null).SetComplexity(10);   // cost 10 + sum(children)
});

schema.Query()
    .AddField("expensiveReport", ctx => ctx.GenerateReport(), "Run the big report")
    .SetComplexity(50);
```

**Attribute** — decorate the C# property or method directly. Works on query fields, mutations, and subscription fields:

```cs
using EntityGraphQL.Schema.QueryLimits;

public class QueryContext
{
    [FieldComplexity(50)]
    public Report GenerateReport() => ...;
}

public class MyMutations : IMutations
{
    [GraphQLMutation]
    [FieldComplexity(75)]
    public bool ExpensiveOperation() => ...;
}
```

The attribute is equivalent to calling `SetComplexity(n)` and can be combined with `[GraphQLMutation]` or any other field attribute. The fixed value replaces the default base cost of `1`. Child fields still contribute.

### Args-aware cost

When cost depends on a page-size argument or some other request parameter, use the calculator form. The calculator receives its children's pre-computed cost plus access to the field's arguments. There are three ways to reach the args, pick what's cleanest for your field:

**Typed via the chain** — when you chain `SetComplexity` off `AddField`/`ReplaceField` with typed args, the args type flows through automatically. The calculator's `ctx` has a typed `Args` and the `ChildComplexity`:

```cs
schema.Query()
    .ReplaceField("projects", new { take = 10 }, (ctx, args) => ctx.Projects.Take(args.take), "projects")
    .SetComplexity(ctx => ctx.Args.take * (1 + ctx.ChildComplexity));
```

**Typed with a named class** — via `ctx.Args<T>()`:

```cs
public class ProjectsArgs { public int Take { get; set; } }

schema.Query().GetField("projects", null).SetComplexity(ctx =>
{
    var args = ctx.Args<ProjectsArgs>();
    return args.Take * (1 + ctx.ChildComplexity);
});
```

**Ad-hoc by name** — when you need a single arg without defining a type:

```cs
schema.Query().GetField("projects", null).SetComplexity(ctx =>
{
    var rows = Math.Min(ctx.Arg<int>("take"), 1000);
    return 1 + rows * (1 + ctx.ChildComplexity);
});
```

All three forms give you `ctx.ChildComplexity` (the sum of children's cost — typically you multiply it by your row count). **The calculator's return value is the whole cost for that field.** Children are not re-added. This lets you express any model, from "flat cost regardless of children" to "super-linear in children × row count".

:::info Variable-bound args
Argument values sourced from `$variables` are resolved to their real request values before the calculator runs — `ctx.Arg<int>("take")` returns `50` when the client passes `$pageSize = 50`, not the C# default `0`. If a variable is not supplied and has no default, the calculator sees `0` / `null`.
:::

### Custom analyzer

Implement `IQueryComplexityAnalyzer` if you want a different cost model (for example, a cost that takes field depth into account, or a cost sourced from a directive):

```cs
public class MyAnalyzer : IQueryComplexityAnalyzer
{
    public int CalculateComplexity(GraphQLDocument document, string? operationName, ExecutionOptions options)
    {
        // ... walk document.Operations / document.Fragments
        return totalCost;
    }
}

var options = new ExecutionOptions
{
    MaxQueryComplexity = 1000,
    QueryComplexityAnalyzer = new MyAnalyzer(),
};
```

## Error format

A limit violation produces a standard GraphQL error response with no `data`:

```json
{
  "errors": [
    { "message": "Query exceeds maximum allowed depth of 10" }
  ]
}
```

Messages are deterministic and safe to log / alert on.

## Per-field rate limiting

The query-wide limits above stop runaway queries as a whole. For individual fields — say a costly report generator or a third-party service call — tag the field with `.AddRateLimit(policyName)` and register a policy. Acquisition happens before execution; denial returns a GraphQL error and no resolver runs. Leases are held until the query finishes, so concurrency limiters release permits correctly.

### Tag your fields

```cs
using EntityGraphQL.Schema.QueryLimits;

schema.Query()
    .AddField("generateReport", ctx => reportSvc.Build(ctx), "Expensive report")
    .AddRateLimit("expensive-report");

schema.UpdateType<Project>(type =>
{
    // per-user, tighter cap on a specific field
    type.GetField(p => p.BigAnalytics, null).AddRateLimit("analytics", userSpecific: true);
});
```

Multiple `.AddRateLimit(...)` calls on the same field stack — every policy must succeed, so a field can belong to both a global bucket and a user-specific bucket at once.

### Register the default service (ASP.NET)

`EntityGraphQL.AspNet` ships a `PartitionedRateLimiter<string>`-backed default. Register it once in `Program.cs`:

```cs
using EntityGraphQL.AspNet;

builder.Services.AddGraphQLFieldRateLimit(opts =>
{
    opts.AddFixedWindowPolicy("expensive-report", permitLimit: 10, window: TimeSpan.FromMinutes(1));
    opts.AddConcurrencyPolicy("analytics", permitLimit: 2);
    opts.AddTokenBucketPolicy("search", tokenLimit: 50, replenishmentPeriod: TimeSpan.FromSeconds(5), tokensPerPeriod: 5);
    opts.AddSlidingWindowPolicy("per-user-read", permitLimit: 100, window: TimeSpan.FromMinutes(1), segmentsPerWindow: 6);

    // "queries per window" semantics instead of "invocations per window" — see Counting below
    opts.AddFixedWindowPolicy("login", permitLimit: 5, window: TimeSpan.FromMinutes(1), oncePerRequest: true);

    // Custom partition factory for scenarios the helpers don't cover:
    opts.AddPolicy("tiered", key => key.Contains("|pro|")
        ? RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions { TokenLimit = 100, /* ... */ })
        : RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions { TokenLimit = 10,  /* ... */ }));
});
```

The policy names here must match the names passed to `field.AddRateLimit("...")`. Tagging a field with a policy that isn't registered throws `InvalidOperationException` on the first request that hits that field — by design, so configuration typos surface immediately instead of silently passing through.

### Counting: per-selection vs. once-per-request

By default each selection of a rate-limited field charges one permit. A query that selects the same field 10 times (via aliases or fragment spreads) runs the resolver 10 times, so it burns 10 permits. This prevents the obvious abuse of aliasing an expensive field 500 times:

```graphql
{ a: expensive b: expensive c: expensive ... }    # 500 aliases = 500 permits
```

If a query requests more permits than the policy's total capacity (e.g. 10 aliases against a bucket of 5), the request is denied — not partially served.

For policies whose natural unit is "queries per window" rather than "invocations per window" — think login attempts, password resets, or any semantic where "asking counts as one regardless of how many times you wrote the field name" — pass `oncePerRequest: true` on the policy. The service clamps permit count to 1 regardless of selection count.

### User-specific partitioning

When a field is tagged `userSpecific: true`, the service partitions by user. Default key selector is `ClaimsPrincipal.Identity?.Name`. Override via `ExecutionOptions.UserKeySelector` to use an API-key header or a custom claim:

```cs
var options = new ExecutionOptions
{
    UserKeySelector = user => user?.FindFirst("api-key")?.Value,
};
```

Each unique `(policy, userKey)` combination gets its own limiter instance. For very large user populations this grows unbounded — partitions are not evicted. If you expect millions of distinct users, either avoid `userSpecific: true` on hot fields or replace `IFieldRateLimitService` with a distributed / LRU-bounded implementation.

### Distributed / custom backends

The default is single-node, in-memory. For multi-instance deployments where all app servers should share a bucket, implement `IFieldRateLimitService` yourself and register it before calling `AddGraphQLFieldRateLimit` (the default uses `TryAddSingleton` so a prior registration wins):

```cs
services.AddSingleton<IFieldRateLimitService, RedisFieldRateLimitService>();
services.AddGraphQLFieldRateLimit(opts => { /* policies are ignored when the above is registered */ });
```

Or skip `AddGraphQLFieldRateLimit` entirely and register only your implementation.

### Where this fits relative to route-level limiting

- Route-level middleware (ASP.NET's `app.UseRateLimiter()` at `/graphql`) caps total traffic per client.
- Per-field rate limiting here protects specific expensive resolvers regardless of how much cheap traffic is flowing on the same endpoint.
- Use both — they're complementary.

## What this does not cover

- **Runtime enforcement inside resolvers.** These guards are pre-execution only. If a single resolver is slow you still want timeouts and bulkheads inside that resolver.
- **Schema-level authorization.** Use `RequiredAuthorization` / `[GraphQLAuthorize]` for that; limits here are orthogonal.
