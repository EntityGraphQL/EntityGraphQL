---
sidebar_position: 10
---

# Tool integration

Being GraphQL there are many tools that integrate well with EntityGraphQL.

EntityGraphQL supports GraphQL introspection queries so tools like GraphiQL etc can work against your schema.

You can use `schema.ToGraphQLSchemaString()` to produce a GraphQL schema file. This works well as input to the Apollo code gen tools.
