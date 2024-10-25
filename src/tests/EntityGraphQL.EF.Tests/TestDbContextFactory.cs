using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EntityGraphQL.EF.Tests;

internal class TestDbContextFactory : IDisposable
{
    private SqliteConnection? connection;

    private DbContextOptions<TestDbContext> CreateOptions(Func<DbContextOptionsBuilder<TestDbContext>, DbContextOptionsBuilder<TestDbContext>>? config = null)
    {
        var builder = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection!);
        if (config != null)
        {
            builder = config(builder);
        }
        return builder.Options;
    }

    public TestDbContext CreateContext(Func<DbContextOptionsBuilder<TestDbContext>, DbContextOptionsBuilder<TestDbContext>>? config = null)
    {
        if (connection == null)
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = CreateOptions(config);
            using var context = new TestDbContext(options);
            context.Database.EnsureCreated();
        }

        return new TestDbContext(CreateOptions(config));
    }

    public void Dispose()
    {
        if (connection != null)
        {
            connection.Dispose();
            connection = null;
        }
    }
}
