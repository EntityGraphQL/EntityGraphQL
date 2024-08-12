using EntityGraphQL.AspNet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using subscriptions.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions() { WebRootPath = "wwwroot/dist", Args = args });

var connectionString = "DataSource=chat;mode=memory;cache=shared";
var keepAliveConnection = new SqliteConnection(connectionString);
keepAliveConnection.Open();

builder.Services.AddDbContext<ChatContext>(opt => opt.UseSqlite(connectionString));
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<ChatEventService>();

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
app.UseGraphQLWebSockets<ChatContext>();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGraphQL<ChatContext>();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatContext>();
    db.Database.EnsureCreated();
    db.Database.Migrate();
}

app.Run();
