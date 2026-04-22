using EntityGraphQL.AspNet;
using HotChocolate.AspNetCore;
using Microsoft.AspNetCore.Http;
using tooling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped(_ => ToolingContext.Create());
builder.Services.AddGraphQLSchema<ToolingContext>();

var app = builder.Build();

app.UseStaticFiles();

app.MapGet(
    "/",
    context =>
    {
        context.Response.Redirect("/graphiql");
        return Task.CompletedTask;
    }
);

app.MapGet(
    "/graphiql",
    async context =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync("wwwroot/graphiql/index.html");
    }
);

app.MapGraphQL<ToolingContext>("/api/graphql");

app.MapNitroApp("/nitro", "/api/graphql").WithOptions(new GraphQLToolOptions { GraphQLEndpoint = "/api/graphql", IncludeCookies = true });

app.Run();
