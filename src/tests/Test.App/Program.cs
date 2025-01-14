using EntityGraphQL.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGraphQLSchema<TestQueryType>();
builder.Services.AddScoped<TestQueryType>();

var app = builder.Build();

app.MapGraphQL<TestQueryType>(followSpec: true);

app.Run();

public partial class Program;

public class TestQueryType
{
    public string Hello { get; set; } = "world";
}
