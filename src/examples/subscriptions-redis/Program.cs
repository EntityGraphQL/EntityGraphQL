using EntityGraphQL.AspNet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using subscriptions_redis.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database (SQLite in-memory for demo purposes) ---
var connectionString = "DataSource=chat-redis;mode=memory;cache=shared";
var keepAliveConnection = new SqliteConnection(connectionString);
keepAliveConnection.Open();

builder.Services.AddDbContext<ChatContext>(opt => opt.UseSqlite(connectionString));

// --- Redis ---
// IConnectionMultiplexer is thread-safe and designed to be shared across the
// application. Register it as a singleton so all ChatService instances reuse
// the same connection pool.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddSingleton<ChatService>();

// --- GraphQL ---
builder.Services.AddGraphQLSchema<ChatContext>().ConfigureGraphQLSchema<ChatContext>(ChatGraphQLSchema.ConfigureSchema);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseWebSockets();
app.UseHttpsRedirection();
app.UseRouting();
app.UseGraphQLWebSockets<ChatContext>();
app.MapGraphQL<ChatContext>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatContext>();
    db.Database.EnsureCreated();
}

app.Run();
