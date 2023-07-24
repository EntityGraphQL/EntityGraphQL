---
sidebar_position: 11
---

# Upgrading from 4.x to 5.x

EntityGraphQL respects [Semantic Versioning](https://semver.org/), meaning version 5.0.0 contains breaking changes. Below highlights those changes and the impact to those coming from version 3.x.

:::tip
You can see the full changelog which includes other changes and bug fixes as well as links back to GitHub issues/MRs with more information [here on GitHub](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/CHANGELOG.md).
:::

## `SchemaProvider.ExecuteRequest` change

`SchemaProvider.ExecuteRequest` & `SchemaProvider.ExecuteRequestAsync` no longer take an instance of the schema context. The context will be fetched from the provided `ServiceProvider` meaning the lifetime rules are adhered to - e.g. `ServiceLifetime.Transient` is now correctly used. This is the perferred way to execute a query.

If you wish to maintain the 4.x execution you can use`ExecuteRequestWithContext` & `ExecuteRequestWithContextAsync` which operate in the same way as 4.x - the instance of the schema context passed in will be used for all references to that type.

## Changes to Method Argument Reflection

Previously if `AutoCreateInputTypes` was enabled we didn't know if a parameter should be a GraphQL argument or an injected service unless you used `[GraphQLArguments]`. But this meant you couldn't have complex types as parameters in the method and have them reflected in the schema (`[GraphQLArguments]` flattens the properties in the schema as arguments). This has been refactored to be predictable.

`AutoCreateInputTypes` now defaults to `true` and you will have to add some attributes to your parameters or classes.

`[GraphQLInputType]` will include the parameter as an argument and use the type as an input type. `[GraphQLArguments]` will flatten the properties of that parameter type into many arguments in the schema.

When looking for a methods parameters, EntityGraphQL will

1. First all scalar / non-complex types will be added as arguments in the schema.

2. If parameter type or enum type is already in the schema it will be added at an argument.

3. Any argument or type with `GraphQLInputTypeAttribute` will be added to the schema as an `InputType`

4. Any argument or type with `GraphQLArgumentsAttribute` found will have the types properties added as schema arguments.

5. If no attributes are found it will assume they are services and not add them to the schema. _I.e. Label your arguments with the attributes or add them to the schema beforehand._

`AutoCreateInputTypes` now only controls if the type of the argument should be added to the schema.

## `IExposableException` removed

Interface `IExposableException` has been removed. Use the existing `SchemaBuilderSchemaOptions.AllowedExceptions` property to define which exceptions are rendered into the results. Or mark your exceptions with the `AllowedExceptionAttribute` to have exception details in the results when `SchemaBuilderSchemaOptions.IsDevelopment` is `false`.

## `IDirectiveProcessor` updated

- `IDirectiveProcessor.On` renamed to `IDirectiveProcessor.Location`
- `IDirectiveProcessor.ProcessField()` removed, use `IDirectiveProcessor.VisitNode`
- `IDirectiveProcessor.ProcessExpression()` Has been removed. You can build a new `IGraphQLNode` in `VisitNode` to make changes to the graph

## `SchemaBuilderMethodOptions` removed

- `AutoCreateInputTypes` has been moved to `SchemaBuilderOptions` and is now defaulted to `true`.
- `AddNonAttributedMethods` has been move to `SchemaBuilderOptions.AddNonAttributedMethodsInControllers`


## Register `GraphQLValidator`

`GraphQLValidator` is no longer magically added to your method fields (mutations/subscriptions). If you wish to use it please register it in your services. There is a new helper method in EntityGraphQL.AspNet `AddGraphQLValidator()`. This means you can implement and register your own implementation.