using Microsoft.EntityFrameworkCore;

namespace Benchmarks
{
    public class BenchmarkContext : DbContext
    {
        public DbSet<Movie> Movies { get; set; }
        public DbSet<Person> People { get; set; }
        public DbSet<MovieGenre> Genres { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlite("Data Source=movies.db");
        }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Person>().HasKey(d => d.Id);
            builder.Entity<MovieGenre>().HasKey(d => d.Name);
            builder.Entity<Movie>().HasKey(d => d.Id);
            builder.Entity<Movie>().HasOne(d => d.Director);
        }
    }
}