using System.Collections.Generic;
using System.Linq;

namespace tooling;

public class ToolingContext
{
    public List<Book> Books { get; init; } = [];
    public List<Author> Authors { get; init; } = [];

    public static ToolingContext Create()
    {
        var authors = new List<Author>
        {
            new()
            {
                Id = 1,
                Name = "Ursula K. Le Guin",
                Country = "United States",
            },
            new()
            {
                Id = 2,
                Name = "Octavia E. Butler",
                Country = "United States",
            },
            new()
            {
                Id = 3,
                Name = "N. K. Jemisin",
                Country = "United States",
            },
        };

        var books = new List<Book>
        {
            new()
            {
                Id = 1,
                Title = "A Wizard of Earthsea",
                Genre = Genre.Fantasy,
                PublishedYear = 1968,
                Rating = 9.2,
                AuthorId = 1,
            },
            new()
            {
                Id = 2,
                Title = "The Left Hand of Darkness",
                Genre = Genre.ScienceFiction,
                PublishedYear = 1969,
                Rating = 9.6,
                AuthorId = 1,
            },
            new()
            {
                Id = 3,
                Title = "Kindred",
                Genre = Genre.ScienceFiction,
                PublishedYear = 1979,
                Rating = 9.4,
                AuthorId = 2,
            },
            new()
            {
                Id = 4,
                Title = "Parable of the Sower",
                Genre = Genre.ScienceFiction,
                PublishedYear = 1993,
                Rating = 9.5,
                AuthorId = 2,
            },
            new()
            {
                Id = 5,
                Title = "The Fifth Season",
                Genre = Genre.Fantasy,
                PublishedYear = 2015,
                Rating = 9.3,
                AuthorId = 3,
            },
        };

        foreach (var author in authors)
        {
            author.Books = books.Where(b => b.AuthorId == author.Id).ToList();
        }

        foreach (var book in books)
        {
            book.Author = authors.Single(a => a.Id == book.AuthorId);
        }

        return new ToolingContext { Books = books, Authors = authors };
    }
}

public class Book
{
    public int Id { get; init; }
    public string Title { get; init; } = null!;
    public Genre Genre { get; init; }
    public int PublishedYear { get; init; }
    public double Rating { get; init; }
    public int AuthorId { get; init; }
    public Author Author { get; set; } = null!;
}

public class Author
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Country { get; init; } = null!;
    public List<Book> Books { get; set; } = [];
}

public enum Genre
{
    Fantasy,
    ScienceFiction,
}
