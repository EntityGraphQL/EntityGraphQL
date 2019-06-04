using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace demo
{
    public class DemoContext : DbContext
    {
        public DemoContext(DbContextOptions<DemoContext> opts) : base(opts)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Actor>().HasKey(d => d.PersonId);
            builder.Entity<Writer>().HasKey(d => d.PersonId);
            builder.Entity<Movie>().HasOne(d => d.Director).WithMany(p => p.DirectorOf).HasForeignKey(d => d.DirectorId);
            builder.Entity<Person>().HasMany(p => p.ActorIn).WithOne(a => a.Person);
            builder.Entity<Person>().HasMany(p => p.WriterOf).WithOne(a => a.Person);
        }

        [System.ComponentModel.Description("Collection of Movies")]
        public DbSet<Movie> Movies { get; set; }
        [System.ComponentModel.Description("Collection of Peoples")]
        public DbSet<Person> People { get; set; }
        [System.ComponentModel.Description("Collection of Actors")]
        public DbSet<Actor> Actors { get; set; }
    }

    public class Movie
    {
        public uint Id { get; set; }
        public string Name { get; set; }

        [System.ComponentModel.Description("Enum of Genre")]
        public Genre Genre { get; set; }
        public DateTime Released { get; set; }
        public List<Actor> Actors { get; set; }
        public List<Writer> Writers { get; set; }
        public Person Director { get; set; }
        public uint? DirectorId { get; set; }
        public double Rating { get; internal set; }
    }

    public class Actor
    {
        public uint PersonId { get;  set; }
        public Person Person { get;  set; }
        public uint MovieId { get;  set; }
        public Movie Movie { get;  set; }
    }
    public class Writer
    {
        public uint PersonId { get;  set; }
        public Person Person { get;  set; }
        public uint MovieId { get;  set; }
        public Movie Movie { get;  set; }
    }

    public enum Genre
    {
        [System.ComponentModel.Description("Action movie type")]
        Action,
        [System.ComponentModel.Description("Drama movie type")]
        Drama,
        [System.ComponentModel.Description("Comedy movie type")]
        Comedy,
        [System.ComponentModel.Description("Horror movie type")]
        Horror,
        [System.ComponentModel.Description("Scifi movie type")]
        Scifi,
    }

    public class Person
    {
        public uint Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public List<Actor> ActorIn { get; set; }
        public List<Writer> WriterOf { get; set; }
        public List<Movie> DirectorOf { get; set; }
        public DateTime? Died { get; set; }
        public bool IsDeleted { get; set; }
    }
}