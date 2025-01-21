using EntityGraphQL;
using EntityGraphQL.AspNet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGraphQLValidator();
builder.Services.AddGraphQLSchema<TestQueryType>(configure =>
{
    configure.ConfigureSchema = schema =>
    {
        schema
            .Mutation()
            .Add(
                "mutationFail",
                (TestQueryType db, IGraphQLValidator validator) =>
                {
                    validator.AddError("This is a test error");
                    if (validator.HasErrors)
                    {
                        return null;
                    }
                    return db.Hello;
                }
            )
            .IsNullable(false);
    };
});
builder.Services.AddScoped<TestQueryType>();

var app = builder.Build();

app.MapGraphQL<TestQueryType>(followSpec: true);

app.Run();

public partial class Program;

public class TestQueryType
{
    public string Hello { get; set; } = "world";
}
