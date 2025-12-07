---
sidebar_position: 12
---

# Upgrading from 5.x to 6.x

EntityGraphQL respects [Semantic Versioning](https://semver.org/), meaning version 6.0.0 contains breaking changes. Below highlights those changes and the impact to those coming from version 5.x.

:::tip
You can see the full changelog which includes other changes and bug fixes as well as links back to GitHub issues/MRs with more information [here on GitHub](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/CHANGELOG.md).
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
// From-to converter (bidirectional)
schema.AddTypeConverter<SourceType, TargetType>(
    source => ConvertTo(source),
    target => ConvertFrom(target)
);

// To-only converter (one direction)
schema.AddTypeConverter<SourceType, TargetType>(
    source => ConvertTo(source)
);

// From-only converter (for input types)
schema.AddInputTypeConverter<SourceType, TargetType>(
    target => ConvertFrom(target)
);
```

See the updated documentation for more flexible converting methods available.

## `ExecutableDirectiveLocation` Enum Renamed

Fields in the `ExecutableDirectiveLocation` enum have been renamed to follow C# naming conventions (PascalCase instead of SCREAMING_SNAKE_CASE).

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

## `IFieldExtension.GetExpressionAndArguments` Signature Change

The method signature has changed to take the current GraphQL node instead of the parent node.

## Target Framework Changes

The following target frameworks have been dropped:

- `EntityGraphQL.AspNet` package: Dropped `net6.0` and `net7.0`
- `EntityGraphQL` package: Dropped `net6.0` (but still targets `netstandard2.1`, so it can still be used with those versions)
