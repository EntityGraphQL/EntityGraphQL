---
sidebar_position: 12
---

# Upgrading from 5.x to 6.x

EntityGraphQL respects [Semantic Versioning](https://semver.org/), meaning version 6.0.0 contains breaking changes. Below highlights those changes and the impact to those coming from version 5.x.

:::tip
You can see the full changelog which includes other changes and bug fixes as well as links back to GitHub issues/MRs with more information [here on GitHub](https://github.com/EntityGraphQL/EntityGraphQL/blob/main/CHANGELOG.md).
:::

## Partial Results Support

EntityGraphQL now properly follows the GraphQL spec regarding partial results. Previously if any field failed, the entire operation would fail. Now:

- Each top-level field in the operation is executed separately
- If any fields fail, you'll get partial results from those that succeeded plus error information about the failed ones
- `AddGraphQLValidator` now registers `IGraphQLValidator` as `Transient` (this was the original intent). If you want the old behavior, remove the use of `AddGraphQLValidator` and register `IGraphQLValidator` yourself as `Scoped`
- As per spec, if an error prevented a valid response, the "data" entry will be `null`

## Schema Configuration Options Refactored

The options for configuring schemas have been reorganized for better clarity between schema builder reflection behavior and schema provider configuration.

### `SchemaBuilderSchemaOptions` renamed to `SchemaProviderOptions`

The class has been renamed to better reflect that it configures the schema provider, not the builder.

**Before (5.x):**

```cs
var schema = SchemaBuilder.FromObject<DemoContext>(
    schemaOptions: new SchemaBuilderSchemaOptions { ... }
);
```

**After (6.x):**

```cs
var schema = SchemaBuilder.FromObject<DemoContext>(
    schemaOptions: new SchemaProviderOptions { ... }
);
```

### `AddGraphQLOptions` now uses composition

`AddGraphQLOptions<TContext>` has been refactored from inheritance to composition, making it clearer which options control what.

**Before (5.x):**

```cs
services.AddGraphQLSchema<DemoContext>(options => {
    // All options were at the top level
    options.FieldNamer = name => name;
    options.AutoCreateFieldWithIdArguments = true;
    options.IntrospectionEnabled = true;
    options.AuthorizationService = new CustomAuthService();
});
```

**After (6.x):**

```cs
services.AddGraphQLSchema<DemoContext>(options => {
    // Builder options control reflection/auto-creation behavior
    options.Builder.AutoCreateFieldWithIdArguments = true;
    options.Builder.AutoCreateEnumTypes = true;
    options.Builder.IgnoreProps.Add("MyProp");
    options.Builder.PreBuildSchemaFromContext = schema => {
        // Set up type mappings before reflection
    };

    // Schema options control schema provider configuration
    options.Schema.FieldNamer = name => name;
    options.Schema.IntrospectionEnabled = true;
    options.Schema.AuthorizationService = new CustomAuthService();
    options.Schema.IsDevelopment = false;
})
.ConfigureGraphQLSchema<DemoContext>(schema =>
{
    // Schema-level concerns (types, fields, directives, auth rules, etc.)
});
```

### `AddGraphQLSchema()` now returns a builder

When using `EntityGraphQL.AspNet`, `AddGraphQLSchema()` now returns a dedicated `GraphQLSchemaBuilder<TContext>` instead of `IServiceCollection`.

This enables a more ASP.NET-like fluent setup where service registration concerns stay in the `AddGraphQLSchema()` options callback, and schema-shape concerns are chained with `ConfigureGraphQLSchema(...)`.

**In 6.x:**

```cs
services.AddGraphQLSchema<DemoContext>(options => {
    options.Schema.IntrospectionEnabled = false;
})
.ConfigureGraphQLSchema<DemoContext>(schema => {
    schema.AddType<MyType>("MyType", "...");
});
```

If schema setup needs DI services at build time:

```cs
services.AddGraphQLSchema<DemoContext>()
    .ConfigureGraphQLSchema<DemoContext>((schema, services) => {
        var metadata = services.GetRequiredService<IMetadataService>();
        // use metadata while configuring the schema
    });
```

### `AddGraphQLOptions.ConfigureSchema` is now method-based

`AddGraphQLOptions<TContext>` no longer exposes settable schema-configuration delegate properties. Instead it now provides overloaded `ConfigureSchema(...)` methods.

If you configure `AddGraphQLOptions<TContext>` directly, update:

**Before (5.x):**

```cs
services.AddGraphQLSchema<DemoContext>(options => {
    options.ConfigureSchema = schema => {
        schema.AddType<MyType>("MyType", "...");
    };
});
```

**After (6.x):**

```cs
services.AddGraphQLSchema<DemoContext>(options => {
    options.ConfigureSchema(schema => {
        schema.AddType<MyType>("MyType", "...");
    });
});
```

And if schema setup needs DI services:

```cs
services.AddGraphQLSchema<DemoContext>(options => {
    options.ConfigureSchema((schema, services) => {
        var metadata = services.GetRequiredService<IMetadataService>();
    });
});
```

### `introspectionEnabled` parameter removed from `AddGraphQLSchema`

The `introspectionEnabled` parameter has been removed from the `AddGraphQLSchema` extension method.

**Before (5.x):**

```cs
services.AddGraphQLSchema<DemoContext>(introspectionEnabled: false);
```

**After (6.x):**

```cs
services.AddGraphQLSchema<DemoContext>(options => {
    options.Schema.IntrospectionEnabled = false;
});
```

### ASP.NET Auto-Configuration

When using `AddGraphQLSchema` in ASP.NET, the following defaults are now automatically applied:

- `AuthorizationService` defaults to `PolicyOrRoleBasedAuthorization` (previously required explicit configuration)
- `IsDevelopment` is automatically set to `false` in non-Development environments

## Custom Type Converters

The type converter system has been redesigned for more flexibility.

### `ICustomTypeConverter` removed

The `ICustomTypeConverter` interface has been removed. Use the new generic custom type converter methods on `SchemaProvider` instead:

**Before (5.x):**

```cs
public class MyConverter : ICustomTypeConverter
{
    // implementation
}

schema.AddCustomTypeConverter(new MyConverter());
```

**After (6.x):**

```cs
// From-to converter
schema.AddCustomTypeConverter<SourceType, TargetType>(
    (source, schema) => ConvertTo(source)
);

// To-only converter
schema.AddCustomTypeConverter<TargetType>(
    (value, schema) => ConvertToTarget(value)
);

// From-only converter
schema.AddCustomTypeConverter<SourceType>(
    (value, toType, schema) => ConvertFrom(value, toType),
    typeof(TargetType)
);
```

See the updated documentation for more flexible converting methods available.

## Removed Obsolete Methods

The following methods and properties marked as obsolete in previous versions have been removed:

- `IField.UseArgumentsFromField` - use `GetExpressionAndArguments` instead
- `IField.UseArgumentsFrom` - use `GetExpressionAndArguments` instead
- `IField.ResolveWithService` - use `Resolve` instead
- `IFieldExtension.GetExpression` - use `GetExpressionAndArguments` instead

## `MapGraphQL` Default Behavior

`MapGraphQL` now defaults to the previous `followSpec = true`, which follows https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md.

## Filter Support via `UseFilter`

You can no longer add filter support by using `ArgumentHelper.EntityQuery` or `EntityQueryType` in field args.

**Before (5.x):**

```cs
schemaProvider.Query().ReplaceField("users",
    new { filter = ArgumentHelper.EntityQuery<User>() },
    "Users optionally filtered"
);
```

**After (6.x):**

```cs
schemaProvider.Query()
    .GetField("users")
    .UseFilter();
```

The `UseFilter` extension now supports filters referencing service fields.

## Date Scalar Type Renamed to DateTime

The built-in scalar type for `System.DateTime` has been renamed from `"Date"` to `"DateTime"` to better reflect that it includes time information and to avoid confusion with the `DateOnly` scalar type.

**Before (5.x):**

```graphql
type Person {
  name: String!
  birthDate: Date
}
```

**After (6.x):**

```graphql
type Person {
  name: String!
  birthDate: DateTime
}
```

**Migration Steps:**

1. Update your GraphQL queries to use `DateTime` instead of `Date`
2. If you have custom schema introspection or code generation tools, update them to recognize `DateTime`
3. The CLR type remains `System.DateTime` - only the GraphQL scalar name has changed

## Authorization Refactoring

The authorization system has been refactored to use a keyed data structure for better extensibility. This allows any package to add custom authorization requirements without modifying core classes. **Role-based authorization methods are now extension methods**, following the same pattern as policy-based authorization.

### `RequiredAuthorization` Changes

`RequiredAuthorization` is now a pure data container that uses a keyed data dictionary (`AuthData`). All authorization logic is provided via extension methods.

**Key Changes:**

- `RequiresAllPolicies`, `RequiresAnyPolicy`, etc. have been moved to the `EntityGraphQL.AspNet` package as extension methods
- `RequiresAllRoles()` and `RequiresAnyRole()` are now extension methods on `IField` and `SchemaType<T>` (from `RoleAuthorizationExtensions`)
- Roles are stored under the `"egql:core:roles"` key - use `GetRoles()` extension method to retrieve them
- Policies (in EntityGraphQL.AspNet) are stored under the `"egql:aspnet:policies"` key - use `GetPolicies()` extension method to retrieve them

## `IFieldExtension.GetExpressionAndArguments` Signature Change

The field extension API changed across the 6.0 betas and the final shape uses context objects instead of long positional parameter lists.

### Before (5.x / early 6.0 betas)

Field extensions received long method signatures and `GetExpressionAndArguments()` previously changed to take the current field node instead of the parent node.

### After (final 6.x API)

`IFieldExtension` compilation hooks now use context objects:

- `GetExpressionAndArguments()` takes `FieldExtensionExpressionContext`
- `ProcessExpressionPreSelection()` takes `FieldExtensionPreSelectionContext`
- `ProcessExpressionSelection()` takes `FieldExtensionSelectionContext`

This is a breaking change for custom field extensions.

## `ExecutableDirectiveLocation` enum and introspection naming

`ExecutableDirectiveLocation` enum values were renamed to C#-style `PascalCase` names.

**Before (5.x):**

```cs
ExecutableDirectiveLocation.QUERY
ExecutableDirectiveLocation.FIELD
```

**After (6.x):**

```cs
ExecutableDirectiveLocation.Query
ExecutableDirectiveLocation.Field
```

Introspection output still follows the GraphQL spec names (`QUERY`, `FIELD`, etc.), but C# enum references must use the new names.

## Default Async Concurrency Limit

`ExecutionOptions.MaxQueryConcurrency` now defaults to `100` (previously unlimited). Async fields resolved for lists run per item, so this caps how many resolver calls run concurrently within one query execution.

**Impact:** if you relied on more than 100 concurrent async operations in a single query, set `MaxQueryConcurrency = null` (unlimited) or a higher value. Most workloads will not notice the default.

## Authorization Now Fails Closed

A bare `[GraphQLAuthorize]` / `[Authorize]` attribute (no roles or policies) now requires an authenticated user — previously it was silently ignored, granting anonymous access. `IsAuthorized` requires `Identity.IsAuthenticated` whenever any authorization requirement is present, and in `EntityGraphQL.AspNet` a policy-protected field is denied (rather than allowed through) when no `IAuthorizationService` is registered or the user is `null`.

**Impact:** if anything in your schema relied on a bare authorize attribute being a no-op, or policy checks passing without a registered `IAuthorizationService`, those requests will now be denied. Fields/types with no authorization attribute are unaffected.

## Introspection Respects Authorization

Introspection queries (`__schema` / `__type`) now only return the types and fields the requesting user is authorized to access. Anonymous users can no longer enumerate protected type/field/argument names. `__type(name: ...)` also now returns `null` for an unknown (or unauthorized) type name per the GraphQL spec, instead of an error.

**Impact:** introspection output can differ per user. Tooling that fetches the schema via introspection (codegen, IDEs) should run as a user that can see everything — or use `schema.ToGraphQLSchemaString()`, which is unchanged and always outputs the full schema.

## Compiler Internals Made `internal`

`LinqRuntimeTypeBuilder`, `ExpressionReplacer` and `ExpressionUtil.ListToSingleMethods` are no longer public — they are engine internals with no supported external use. `ParameterReplacer` and the rest of `ExpressionUtil` remain public as they are part of the field extension API, however `ParameterReplacer.ReplaceByType` is marked obsolete (removal in 7.0) as it can capture parameters you did not intend.

## ASP.NET schema lifetime

`AddGraphQLSchema()` in `EntityGraphQL.AspNet` now supports configuring the registered schema lifetime via `options.SchemaLifetime`.

This is useful when schema construction depends on scoped/request-specific services.

```cs
services.AddGraphQLSchema<DemoContext>(options =>
{
    options.SchemaLifetime = ServiceLifetime.Scoped;
});
```

## Renamed `ExecutionOptions`

| 5.x                                | 6.x                                         |
| ---------------------------------- | ------------------------------------------- |
| `ExecutionOptions.MaxQueryNodes`   | `ExecutionOptions.MaxFieldSelections`       |
| `ExecutionOptions.UserKeySelector` | `ExecutionOptions.RateLimitUserKeySelector` |

`MaxFieldSelections` counts field selections (the GraphQL spec term) after fragment expansion. `RateLimitUserKeySelector` reflects that it only affects per-field rate limiters tagged `userSpecific: true`.

## Stricter GraphQL Spec Validation

Documents that were previously accepted but are invalid per the (September 2025) GraphQL spec are now rejected with a validation error:

- Duplicate non-repeatable directives at one location (e.g. `@skip(...) @skip(...)`)
- Conflicting field selection merges — selections with the same response name must be the same field with identical arguments (`{ x: id x: name }` or `{ project(id: 1) project(id: 2) }` are errors; use different aliases), and response shapes across mutually exclusive fragments must be compatible
- `@skip` / `@include` on the root field of a subscription operation
- Deprecating a required argument (schema build error)

**Impact:** clients sending such documents will start receiving errors — they were invalid all along, but check for them in logs before upgrading.

## JSON Serializer Defaults

`DefaultGraphQLResponseSerializer` and `DefaultGraphQLRequestDeserializer` now use `JsonSerializerDefaults.Web`. Request deserialization becomes case-insensitive and reads numbers from JSON strings. Response output is unchanged (it was already camelCase).

## Async Argument Validators

`IField.Validators` is now `IReadOnlyCollection<Func<ArgumentValidatorContext, Task>>` and async validators (`IArgumentValidator` / the `Func<ArgumentValidatorContext, Task>` overload of `AddValidator`) are genuinely awaited for mutation and subscription arguments. Validators on query field arguments run during (synchronous) query compilation — avoid I/O in those. Only affects you if you enumerate/invoke `IField.Validators` directly.

## `BeforeExecuting` Expression Shape

If you use `ExecutionOptions.BeforeExecuting` to inspect or rewrite expressions: root-level list fields on the database-bound pass no longer end in an in-tree `ToList()` call. The deferred query is materialized after the expression executes — asynchronously (honouring the request's `CancellationToken`) when the LINQ provider's query objects implement `IAsyncEnumerable<T>`, as EF Core's do.

## Dependency Updates

`Humanizer.Core` was updated from 2.14.1 to 3.x. It is used to singularize names when generating schemas from your types — inflection rule changes between major versions could alter a generated name in rare cases. Compare `schema.ToGraphQLSchemaString()` output before/after upgrading if generated names matter to your clients.

## New in 6.x Worth Adopting

Not breaking — but if you are touching your schema code anyway:

- **Async fields** — first-class `ResolveAsync<TService>()` with end-to-end `CancellationToken` support and per-field/service/query concurrency limits. See [Async Fields](./schema-creation/async-fields).
- **Query limits** — opt-in `MaxQueryDepth`, `MaxFieldSelections`, `MaxFieldAliases`, `MaxQueryComplexity` and per-field rate limiting. See [Query limits](./schema-creation/query-limits).
- **`UseAggregate()`** — `count`/`min`/`max`/`sum`/`average` over collection fields, translated to a single SQL query under EF. See [Aggregates](./field-extensions/aggregate).
- **`AddFieldsFrom<T>()` / `AddQueryFieldsFrom<T>()`** — group related field definitions into classes with `[GraphQLField]` methods. The class declares its context type by implementing `IFieldsFor<TContext>`, so adding it to the wrong schema type is a compile error. Methods can also return `Expression<Func<TContext, ...>>` to compose fully into the database query. See [Fields](./schema-creation/fields#grouping-fields-with-addfieldsfrom).
- **Paging performance** — `hasNextPage` uses a cheap `EXISTS` query instead of `COUNT(*)` when the total isn't requested.
- **Filter improvements** — filters support GraphQL variables (`$var`), service fields, `selectMany` and filtering by paged child fields.

## Target Framework Changes

Both packages now target `net8.0`, `net9.0` and `net10.0` only:

- `EntityGraphQL.AspNet` package: Dropped `net6.0` and `net7.0`
- `EntityGraphQL` package: Dropped `net6.0` and `netstandard2.1`. Dropping netstandard also removes the `System.Text.Json`, `Microsoft.CSharp` and `System.ComponentModel.Annotations` package dependencies that only that target required.
