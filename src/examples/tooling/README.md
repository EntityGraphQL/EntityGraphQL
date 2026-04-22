# GraphQL Tooling Example

This example shows how to use browser IDEs against an `EntityGraphQL.AspNet` endpoint:

- the `GraphiQL` app served from `/graphiql/`
- the `Hot Chocolate Nitro` app served from `/nitro/`
- `EntityGraphQL` API served from `/api/graphql`

The important part is that both UI apps are separate from the API. The GraphQL endpoint itself is still provided by `EntityGraphQL.AspNet`.

## Run

```bash
dotnet run --launch-profile https --project src/examples/tooling/tooling.csproj
```

Then open the HTTPS URLs printed by ASP.NET in the console, for example:

- `https://localhost:7001/graphiql/`
- `https://localhost:7001/nitro/`

Both tools are configured to execute requests against:

- `https://localhost:7001/api/graphql`

If your machine does not yet trust the local ASP.NET development certificate, run:

```bash
dotnet dev-certs https --trust
```

## Key setup

`Program.cs` shows the relevant integration:

```csharp
app.MapGraphQL<ToolingContext>("/api/graphql");

app.MapNitroApp("/nitro")
    .WithOptions(new GraphQLToolOptions
    {
        GraphQLEndpoint = "/api/graphql",
        IncludeCookies = true,
    });
```

This lets you use Nitro without hosting a Hot Chocolate GraphQL server.

The hosted GraphiQL app lives in:

- `src/examples/tooling/wwwroot/graphiql/index.html`
- `src/examples/tooling/wwwroot/graphiql/site.js`
