using System;
using System.Collections.Generic;
using System.ComponentModel;
using EntityFrameworkCore.Projectables;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.EntityFrameworkCore;

namespace demo
{
    public class DemoContext : DbContext
    {
        public DemoContext(DbContextOptions<DemoContext> opts) : base(opts)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Actor>().HasKey(d => d.PersonId);
            builder.Entity<Writer>().HasKey(d => d.PersonId);
            builder.Entity<Movie>().HasOne(d => d.Director).WithMany(p => p.DirectorOf).HasForeignKey(d => d.DirectorId);
            builder.Entity<Person>().HasMany(p => p.ActorIn).WithOne(a => a.Person);
            builder.Entity<Person>().HasMany(p => p.WriterOf).WithOne(a => a.Person);
        }

        [Description("Collection of Movies")]
        [UseFilter]
        [UseSort]
        [UseConnectionPaging]
        public DbSet<Movie> Movies { get; set; }
        [Description("Collection of Peoples")]
        public DbSet<Person> People { get; set; }
        [Description("Collection of Actors")]
        public DbSet<Actor> Actors { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>{
            {"key1", "value1"},
            {"key2", "value2"},
            {"key3", "value3"},
        };
    }

    public class Movie
    {
        public uint Id { get; set; }
        public string Name { get; set; }

        [Description("Enum of Genre")]
        public virtual Genre Genre { get; set; }
        public virtual DateTime Released { get; set; }
        public virtual List<Actor> Actors { get; set; }
        public virtual List<Writer> Writers { get; set; }
        public virtual Person Director { get; set; }
        public uint? DirectorId { get; set; }
        public double Rating { get; set; }
        public uint CreatedBy { get; set; }

        [GraphQLField]
        [Projectable]
        public uint DirectorAgeAtRelease => (uint)((Released - Director.Dob).Days / 365);

        [GraphQLField]
        public uint[] AgesOfActorsAtRelease()
        {
            var ages = new List<uint>();
            foreach (var actor in Actors)
            {
                ages.Add((uint)((Released - actor.Person.Dob).Days / 365));
            }
            return ages.ToArray();
        }
    }

    public class Actor
    {
        public uint PersonId { get; set; }
        public virtual Person Person { get; set; }
        public uint MovieId { get; set; }
        public virtual Movie Movie { get; set; }
    }
    public class Writer
    {
        public uint PersonId { get; set; }
        public virtual Person Person { get; set; }
        public uint MovieId { get; set; }
        public virtual Movie Movie { get; set; }
    }

    public enum Genre
    {
        [Description("Action movie type")]
        Action,
        [Description("Drama movie type")]
        Drama,
        [Description("Comedy movie type")]
        Comedy,
        [Description("Horror movie type")]
        Horror,
        [Description("Scifi movie type")]
        Scifi,
    }

    public class Person
    {
        public uint Id { get; set; }
        [GraphQLNotNull]
        public string FirstName { get; set; }
        [GraphQLNotNull]
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public virtual List<Actor> ActorIn { get; set; }
        public virtual List<Writer> WriterOf { get; set; }
        public virtual List<Movie> DirectorOf { get; set; }
        public DateTime? Died { get; set; }
        public bool IsDeleted { get; set; }
    }
}