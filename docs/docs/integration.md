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

## GraphQL IDEs

EntityGraphQL does not ship its own browser IDE, but any GraphQL IDE can point at your `MapGraphQL()` endpoint as long as introspection is enabled and the tool is configured to call the same HTTP endpoint.

See the full [tooling example](https://github.com/EntityGraphQL/EntityGraphQL/tree/master/src/examples/tooling) on GitHub. It hosts:

- `EntityGraphQL` at `/api/graphql`
- the `GraphiQL` app at `/graphiql/`
- the `Hot Chocolate Nitro` app at `/nitro/`

### GraphiQL

The example project serves a dedicated GraphiQL app from static assets and points it at the EntityGraphQL endpoint:

```cs
app.UseStaticFiles();
app.MapGraphQL<MyContext>("/api/graphql");

// GraphiQL app served from wwwroot/graphiql/
app.MapGet("/", context =>
{
    context.Response.Redirect("/graphiql/");
    return Task.CompletedTask;
});
```

See `src/examples/tooling/wwwroot/graphiql/` for the hosted GraphiQL app files.

### Hot Chocolate Nitro

The same example project hosts Nitro on a separate route and points it at the existing EntityGraphQL endpoint:

```cs
using EntityGraphQL.AspNet;
using HotChocolate.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGraphQLSchema<MyContext>();

var app = builder.Build();

app.MapGraphQL<MyContext>("/graphql");

app.MapNitroApp("/nitro")
    .WithOptions(new GraphQLToolOptions
    {
        GraphQLEndpoint = "/api/graphql",
        IncludeCookies = true,
    });

app.Run();
```

For Nitro, use a relative `GraphQLEndpoint` when the UI and API are served from the same ASP.NET application. If the UI is hosted elsewhere, use the full absolute URL of the EntityGraphQL endpoint instead.

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
