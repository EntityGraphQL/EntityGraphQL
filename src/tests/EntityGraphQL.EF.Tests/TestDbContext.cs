using Microsoft.EntityFrameworkCore;

namespace EntityGraphQL.EF.Tests;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Actor> Actors { get; set; } = null!;
    public DbSet<Director> Directors { get; set; } = null!;
    public DbSet<Movie> Movies { get; set; } = null!;
    public DbSet<ExternalIdentifier> ExternalIdentifiers { get; internal set; } = null!;
}

public class ExternalIdentifier(string entityName, string externalIdName)
{
    public int Id { get; set; }
    public string ExternalIdName { get; set; } = externalIdName;
    public string EntityName { get; set; } = entityName;
    public int EntityId { get; set; }
}

public interface IEntityWithId
{
    int Id { get; set; }
}

public class Actor(string name) : IEntityWithId
{
    public int Id { get; set; }
    public string Name { get; set; } = name;
    public DateTime Birthday { get; set; }
}

public class Director(string name) : IEntityWithId
{
    public int Id { get; set; }
    public string Name { get; set; } = name;
}

public class Movie(string name) : IEntityWithId
{
    public int Id { get; set; }
    public string Name { get; set; } = name;
    public int? DirectorId { get; set; }
    public Director? Director { get; set; }
    public DateTime Released { get; set; }
    public List<Actor> Actors { get; set; } = [];
}
