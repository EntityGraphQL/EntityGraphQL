using Microsoft.EntityFrameworkCore;

namespace EntityGraphQL.EF.Tests;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options) { }

    public DbSet<Actor> Actors { get; set; }
    public DbSet<Director> Directors { get; set; }
    public DbSet<Movie> Movies { get; set; }
    public DbSet<ExternalIdentifier> ExternalIdentifiers { get; internal set; }
}

public class ExternalIdentifier
{
    public int Id { get; set; }
    public string ExternalIdName { get; set; }
    public string EntityName { get; set; }
    public int EntityId { get; set; }
}

public interface IEntityWithId
{
    int Id { get; set; }
}

public class Actor : IEntityWithId
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime Birthday { get; set; }
}

public class Director : IEntityWithId
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Movie : IEntityWithId
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? DirectorId { get; set; }
    public Director Director { get; set; }
    public DateTime Released { get; set; }
    public List<Actor> Actors { get; set; }
}
