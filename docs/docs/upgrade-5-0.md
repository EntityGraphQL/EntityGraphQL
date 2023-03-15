---
sidebar_position: 11
---

# Upgrading from 4.x to 5.x

EntityGraphQL aims to respect [Semantic Versioning](https://semver.org/), meaning version 5.0.0 contains breaking changes. Below highlights those changes and the impact to those coming from version 3.x.

:::tip
You can see the full changelog which includes other changes and bug fixes as well as links back to GitHub issues/MRs with more information [here on GitHub](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/CHANGELOG.md).
:::

## `IExposableException` removed

Interface `IExposableException` has been removed. Use the existing `SchemaBuilderSchemaOptions.AllowedExceptions` property to define which exceptions are rendered into the results. Or mark your exceptions with the `AllowedExceptionAttribute` to have exception details in the results when `SchemaBuilderSchemaOptions.IsDevelopment` is `false`.
