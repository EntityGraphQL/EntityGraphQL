---
sidebar_position: 12
---

# Upgrading from 3.x to 4.x

EntityGraphQL aims to respect [Semantic Versioning](https://semver.org/), meaning version 4.0.0 contains breaking changes. Below highlights those changes and the impact to those coming from version 3.x.

:::tip
You can see the full changelog which includes other changes and bug fixes as well as links back to GitHub issues/MRs with more information [here on GitHub](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/CHANGELOG.md).
:::

## Renamed Classes

`MutationArgumentsAttribute` renamed to `GraphQLArgumentsAttribute` and is used with subscriptions or mutaiton method arguments. This should be a simple find and replace.

## `AddMutationsFrom` Argument Changes

`AddMutationsFrom` and friends now take an optional `SchemaBuilderMethodOptions` object to configure how mutations are built, following the `SchemaBuilderOptions` used elsewhere. The previous overrides have been removed. Important defaults for `SchemaBuilderMethodOptions`:

```cs
bool AutoCreateInputTypes = false; // Any input types seen will be added to the schema if true
bool AddNonAttributedMethods = false; // GraphQLMutationAttributes are still required by default
bool AutoCreateNewComplexTypes = true; // Return types of mutations will be added to the schema
```

## `DateTimeOffset` in Default Schema

When you create a `SchemaProvider<T>`, the `DateTimeOffset` Dotnet type is now added as a default scalar type. It is a very common used type in EF contexts and trips up new users. If you previously were adding it as a scalar type you no longer need to. Or if you'd like to add it differently (like map it to Date) you can

```cs
services.AddGraphQLSchema<DemoContext>(options =>
{
    options.PreBuildSchemaFromContext = (schema) =>
    {
        schema.RemoveType<DateTimeOffset>();
        // add it how you want
    };
});
```

## `SchemaBuilderOptions.IgnoreTypes` Change

`SchemaBuilderOptions.IgnoreTypes` Now uses `Type` instead of `string`. It is a `HashSet<Type>` now to avoid confusion of which name to use (full name space or not).

## Field Extensions

`ProcessExpressionSelection` used in Field Extentions now takes `Dictionary<IFieldKey, CompiledField>` for the `selectionExpressions` parameter. `IFieldKey` is the field name and the schema type the field belongs too. Helps when dealing with inline fragments/union types where we may have multiple fields with the same name from different types.