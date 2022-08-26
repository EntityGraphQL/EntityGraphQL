using EntityGraphQL.AspNet;
using Microsoft.EntityFrameworkCore;
using subscriptions.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
{
    WebRootPath = "wwwroot/dist",
    Args = args
});

builder.Services.AddDbContext<ChatContext>(opt => opt.UseSqlite("Filename=chat.db"));
builder.Services.AddSingleton<ChatService>();

builder.Services.AddGraphQLSchema<ChatContext>(options =>
{
    options.ConfigureSchema = ChatGraphQLSchema.ConfigureSchema;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseWebSockets();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseFileServer();

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGraphQL<ChatContext>();
});

app.UseGraphQLWebSockets<ChatContext>();

app.Run();
