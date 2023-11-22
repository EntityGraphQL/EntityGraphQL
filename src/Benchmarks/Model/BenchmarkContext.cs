using Microsoft.EntityFrameworkCore;

namespace Benchmarks
{
    public class BenchmarkContext : DbContext
    {
        public DbSet<Movie> Movies => Set<Movie>();
        public DbSet<Person> People => Set<Person>();
        public DbSet<MovieGenre> Genres => Set<MovieGenre>();

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
            builder.Entity<Person>().HasMany(d => d.DirectorOf);
        }
    }
}