using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EntityGraphQL.EF.Tests;

internal class TestDbContextFactory : IDisposable
{
    private SqliteConnection? connection;

    private DbContextOptions<TestDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection!).Options;
    }

    public TestDbContext CreateContext()
    {
        if (connection == null)
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = CreateOptions();
            using var context = new TestDbContext(options);
            context.Database.EnsureCreated();
        }

        return new TestDbContext(CreateOptions());
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
