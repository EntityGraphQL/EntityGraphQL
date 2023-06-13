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
    }

    internal class Actor
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    internal class Movie
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int DirectorId { get; set; }
        public DateTime Released { get; set; }
        public List<Actor> Actors { get; set; }
    }
}