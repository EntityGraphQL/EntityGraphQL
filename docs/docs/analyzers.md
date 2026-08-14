---
sidebar_position: 13
---

# Analyzers

EntityGraphQL ships Roslyn analyzers in the main package - there is nothing extra to install. They catch schema-building mistakes in the editor and at build time instead of at start-up or, worse, in production with a particular query shape.

They are useful here because most EntityGraphQL mistakes are invisible in a simple test: a field works with a single object and no services, then behaves differently once it is selected on a list or once two-pass service execution kicks in. The schema is built in ordinary C#, so an analyzer can see the whole fluent chain and check it.

## Rules

### EGQL001 - Service field on an entity type has no bulk resolver {#egql001}

Severity: **Info** · Category: Performance

A `.Resolve<TService>()` field on an entity type runs once per item of any list that selects it - the classic GraphQL N+1. Adding a bulk resolver batches it into one call.

```cs
// reported - one service call per project in the list
schema.Type<Project>().AddField("createdBy", "Creator")
    .Resolve<UserService>((p, srv) => srv.GetUser(p.CreatedById));

// clean - one call for the whole list
schema.Type<Project>().AddField("createdBy", "Creator")
    .Resolve<UserService>((p, srv) => srv.GetUser(p.CreatedById))
    .ResolveBulk<UserService, int, User>(p => p.CreatedById, (ids, srv) => srv.GetAllUsers(ids));
```

Not reported for fields on the `Query`/`Mutation` root, which resolve once per request. Ignore it (or turn it off) when the type is only ever fetched singly, or the service batches or caches internally. See [service fields and bulk resolvers](./schema-creation/other-data-sources).

### EGQL002 - Async field uses a service that is not thread-safe {#egql002}

Severity: **Warning** · Category: Correctness

`ExecutionOptions.MaxQueryConcurrency` defaults to 100, so an async field selected on a list resolves many items **concurrently**. A `DbContext` does not support concurrent use - it throws or corrupts data.

```cs
// reported
schema.Type<Project>().AddField("stats", "Stats").ResolveAsync<MyDbContext>((p, db) => db.LoadStatsAsync(p.Id));

// clean - serialised for this field
schema.Type<Project>().AddField("stats", "Stats").ResolveAsync<MyDbContext>((p, db) => db.LoadStatsAsync(p.Id), maxConcurrency: 1);
```

Alternatives: register a `ServiceConcurrencyLimit` for the service, or resolve a fresh context from `IDbContextFactory` inside the resolver. See [Async fields](./schema-creation/async-fields).

### EGQL003 - Field extension requires a collection field {#egql003}

Severity: **Warning** · Category: Correctness

`UseFilter`, `UseSort`, `UseOffsetPaging`, `UseConnectionPaging` and `UseAggregate` all build on a collection expression. Applying one to a scalar or single-object field throws when the schema is built - this reports it in the editor instead.

```cs
schema.Type<Project>().AddField("name", p => p.Name, "Name").UseFilter();   // reported
schema.Type<Project>().AddField("tasks", p => p.Tasks, "Tasks").UseFilter(); // clean
```

### EGQL004 - Use ResolveAsync for an expression that returns a Task {#egql004}

Severity: **Warning** · Category: Correctness · **Has a code fix**

A synchronous `Resolve()` whose expression returns a `Task<T>` makes the field's GraphQL type the task itself. EntityGraphQL rejects this when the schema is built. The code fix rewrites the call to `ResolveAsync()`.

```cs
schema.Type<Project>().AddField("owner", "Owner").Resolve<UserService>((p, srv) => srv.GetAsync(p.Id));      // reported
schema.Type<Project>().AddField("owner", "Owner").ResolveAsync<UserService>((p, srv) => srv.GetAsync(p.Id)); // clean
```

### EGQL005 - Name is not a valid GraphQL name {#egql005}

Severity: **Warning** · Category: Correctness

GraphQL names may only contain letters, numbers and underscores. A name with a space, dash or dot produces a schema clients cannot query - EntityGraphQL throws for type names, and for field names the invalid name reaches your SDL.

```cs
schema.Type<Project>().AddField("first name", p => p.Name, "d");  // reported
schema.Type<Project>().AddField("firstName", p => p.Name, "d");   // clean
```

### EGQL006 - Subscription method must return IObservable&lt;T&gt; {#egql006}

Severity: **Warning** · Category: Correctness

A subscription produces a stream of events, so a `[GraphQLSubscription]` method must return `IObservable<T>`, `Task<IObservable<T>>` or `ValueTask<IObservable<T>>`. EntityGraphQL throws when the schema is built otherwise.

### EGQL007 - OneOf input type fields must all be nullable {#egql007}

Severity: **Warning** · Category: Correctness

A `[GraphQLOneOf]` input type accepts exactly one of its fields per request, so every field has to be optional.

```cs
[GraphQLOneOf]
public class SearchInput
{
    public int? ById { get; set; }
    public string ByName { get; set; } = "";  // reported - make it string?
}
```

### EGQL008 - Field is added to the same type twice {#egql008}

Severity: **Warning** · Category: Correctness

`AddField` throws when the field already exists. Use `ReplaceField` to redefine one - including fields `SchemaBuilder.FromObject` created for you. Only reported when both calls are in the same method, so unrelated schema-building code is never flagged.

```cs
schema.Type<Project>().AddField("slug", p => p.Name, "d");
schema.Type<Project>().AddField("slug", p => p.Name.ToLower(), "d");     // reported
schema.Type<Project>().ReplaceField("slug", p => p.Name.ToLower(), "d"); // clean
```

### EGQL009 - Use the async execute method inside an async method {#egql009}

Severity: **Info** · Category: Performance

`ExecuteRequest` / `ExecuteRequestWithContext` block the calling thread on the async pipeline. Inside an `async` method, awaiting the `Async` overload costs nothing and frees the thread for the duration of the query. Only reported inside async methods.

### EGQL010 - Resolver blocks on a Task instead of resolving asynchronously {#egql010}

Severity: **Info** · Category: Performance

`.Result`, `.Wait()` and `GetAwaiter().GetResult()` inside a `Resolve()` or `ResolveBulk()` block the executing thread until the call returns - and on a list, once per item. The `Async` overload awaits instead.

```cs
// reported - blocks a thread per project in the list
schema.Type<Project>().AddField("owner", "Owner")
    .Resolve<UserService>((p, srv) => srv.GetAsync(p.Id).Result);

// clean
schema.Type<Project>().AddField("owner", "Owner")
    .ResolveAsync<UserService>((p, srv) => srv.GetAsync(p.Id));
```

This is **Info**, not a warning, because the move is not always free: async resolvers run concurrently across a list (`ExecutionOptions.MaxQueryConcurrency` defaults to 100), so the service has to be safe to use that way. If it is not, pass `maxConcurrency` or register a `ServiceConcurrencyLimit` - see [EGQL002](#egql002). Blocking on a thread you own is the safer of the two mistakes, so this rule nudges rather than insists.

Only reported when the blocking call is written in the resolver's lambda. Blocking inside a helper method the lambda calls is not detected.

## Changing severity

Every rule can be configured per project in `.editorconfig` - raise the ones you want enforced, silence the ones that do not apply:

```ini
[*.cs]
# treat the N+1 warning as a build error
dotnet_diagnostic.EGQL001.severity = error

# this schema never lists these types - turn it off
dotnet_diagnostic.EGQL002.severity = none
```

Or by category:

```ini
dotnet_analyzer_diagnostic.category-EntityGraphQL.Performance.severity = warning
```

To suppress a single occurrence, use the standard pragma or `[SuppressMessage]`:

```cs
#pragma warning disable EGQL001 // the user service caches internally
schema.Type<Project>().AddField("createdBy", "Creator").Resolve<UserService>((p, srv) => srv.GetUser(p.CreatedById));
#pragma warning restore EGQL001
```

## What analyzers do not cover

- Whether an expression translates to SQL - that is provider-specific and belongs to EF.
- The GraphQL documents your clients send - the analyzers only see your schema-building C#.
- Anything that depends on runtime data.
