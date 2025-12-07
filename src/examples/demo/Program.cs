using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using demo;
using EntityGraphQL.AspNet;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Configure database
builder.Services.AddDbContext<DemoContext>(opt => opt.UseSqlite("Filename=demo.db"));

// Add services that will be injected into GraphQL fields
builder.Services.AddSingleton<AgeService>();
builder.Services.AddSingleton<UserService>();

// Configure JSON serialization for the API
builder
    .Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.JsonSerializerOptions.IncludeFields = true;
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Add GraphQL schema - showcasing EntityGraphQL.AspNet features
builder
    .Services.AddGraphQLSchema<DemoContext>(options =>
    {
        // Configure builder options - controls how schema is built from reflection
        options.Builder.IgnoreProps.Add("IsDeleted"); // Don't expose soft-deleted flag
        options.Builder.PreBuildSchemaFromContext = schema =>
        {
            // Add custom scalar types before reflection
            schema.AddScalarType<KeyValuePair<string, string>>("StringKeyValuePair", "Represents a pair of strings");
        };

        options.ConfigureSchema = GraphQLSchema.ConfigureSchema;
    })
    .AddGraphQLValidator(); // Add validation support for mutations

var app = builder.Build();

// Initialize database with sample data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DemoContext>();
    SeedData.Initialize(db);
}

// Serve static files (GraphiQL UI)
app.UseStaticFiles();

// Map GraphQL endpoint with advanced features
app.MapGraphQL<DemoContext>(
    options: new ExecutionOptions
    {
        // Add EF Core query tags for debugging
        BeforeRootFieldExpressionBuild = (exp, op, field) =>
        {
            if (exp.Type.IsGenericTypeQueryable())
                return Expression.Call(
                    typeof(EntityFrameworkQueryableExtensions),
                    nameof(EntityFrameworkQueryableExtensions.TagWith),
                    [exp.Type.GetGenericArguments()[0]],
                    exp,
                    Expression.Constant($"GQL op: {op ?? "n/a"}, field: {field}")
                );
            return exp;
        },
#if DEBUG
        IncludeDebugInfo = true,
#endif
        IncludeQueryInfo = true, // Include query execution metadata
    }
);

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
