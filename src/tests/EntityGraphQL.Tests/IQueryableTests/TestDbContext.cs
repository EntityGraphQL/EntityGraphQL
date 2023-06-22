using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EntityGraphQL.Tests.IQueryableTests
{
    internal class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<Actor> Actors { get; set; }
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

    internal interface IEntityWithId
    {
        int Id { get; set; }
    }

    internal class Actor : IEntityWithId
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    internal class Movie : IEntityWithId
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int DirectorId { get; set; }
        public DateTime Released { get; set; }
        public List<Actor> Actors { get; set; }
    }
}