---
title: "Note on Naming"
metaTitle: "Note on Naming - EntityGraphQL"
metaDescription: "Note on Naming"
---

GraphQL is case sensitive. Currently EntityGraphQL will automatically turn field and argument names from `UpperCase` to `camelCase` when you use the helper methods to create a schema with the default namer factory. This means your C# code matches what C# code typically looks like and your GraphQL matches the GraphQL norm too.

Examples:
- A mutation method in C# named `AddMovie` will be `addMovie` in the schema
- A root field entity named `Movie` will be named `movie` in the schema
- A mutation arguments class (`ActorArgs`) with fields `FirstName` & `Id` will be arguments in the schema as `firstName` & `id`
- If you're using the schema builder manually, the names you give will be the names used. E.g. `schemaProvider.AddField("someEntity", ...)` is different to `schemaProvider.AddField("SomeEntity", ...)`
