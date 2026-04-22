---
sidebar_position: 11
---

# Tool integration

Being GraphQL there are many tools that integrate well with EntityGraphQL.

EntityGraphQL supports GraphQL introspection queries so tools like GraphiQL and Relay can work against your schema.

## Introspection

Introspection is **enabled by default** and is required by most GraphQL client tools (GraphiQL, Insomnia, Relay dev tools, Apollo Studio, code generators, etc.). Disabling it is not recommended unless your schema must be kept confidential.

### Disabling introspection

**ASP.NET** — via `AddGraphQLSchema`:

```cs
builder.Services.AddGraphQLSchema<MyContext>(options =>
{
    options.Schema.IntrospectionEnabled = false;
});
```

**Direct schema creation**:

```cs
var schema = SchemaBuilder.FromObject<MyContext>(
    schemaOptions: new SchemaProviderOptions { IntrospectionEnabled = false }
);
```

### Restricting introspection with authorization

Rather than a blanket disable, you can restrict introspection to authenticated or authorized users. Add `[GraphQLAuthorize]` / `RequiredAuthorization` to the `__schema` and `__type` introspection fields after schema construction:

```cs
builder.Services.AddGraphQLSchema<MyContext>(options =>
{
    options.ConfigureSchema = schema =>
    {
        // require any authenticated user for introspection
        schema.Query().GetField("__schema", null)?.RequireAuthorization();
        schema.Query().GetField("__type", null)?.RequireAuthorization();
    };
});
```

This leaves introspection available to your own developers while hiding it from unauthenticated requests.

You can use `schema.ToGraphQLSchemaString()` to produce a GraphQL schema file. This works well as input to the Apollo code gen tools. If you want to expose that SDL from a custom endpoint or controller, see the custom controller example in [Getting Started](./getting-started#executing-a-query-in-a-custom-controller).

## Query Information & Monitoring

EntityGraphQL can provide detailed information about executed queries through the `QueryInfo` feature. This is useful for:

- Query analysis and optimization
- Debugging complex queries
- Monitoring GraphQL usage patterns
- Understanding which types and fields are being accessed

### Enabling Query Information

To include query execution information in your results, set `IncludeQueryInfo = true` in your execution options:

```cs
var options = new ExecutionOptions
{
    IncludeQueryInfo = true
};

var result = schema.ExecuteRequestWithContext(request, context, serviceProvider, user, options);
```

### ASP.NET Integration

When using EntityGraphQL.AspNet, you can enable query info globally:

```cs
app.MapGraphQL<DemoContext>(options: new ExecutionOptions
{
    IncludeQueryInfo = true
});
```

### Query Information Output

When enabled, query information is included in the `extensions` field of the GraphQL response:

```json
{
  "data": {
    "people": [{ "name": "John", "projects": [{ "name": "Project A" }] }]
  },
  "extensions": {
    "queryInfo": {
      "operationType": "Query",
      "operationName": "GetPeople",
      "totalTypesQueried": 3, // Includes the Query Type
      "totalFieldsQueried": 6,
      "typesQueried": {
        "Query": ["people"],
        "Person": ["name", "projects"],
        "Project": ["name"]
      }
    }
  }
}
```

### Query Information Properties

- **operationType**: The type of GraphQL operation (Query, Mutation, or Subscription)
- **operationName**: The name of the operation (if provided in the query)
- **totalTypesQueried**: Total number of types accessed in the query
- **totalFieldsQueried**: Total number of fields selected across all types
- **typesQueried**: Dictionary mapping type names to the list of fields selected from each type

Note: Fragment spreads are expanded and their fields are counted, but the fragment spread itself is not counted as a field.
