using System;

namespace demo;

/// <summary>
/// Seeds the database with sample movie and people data
/// </summary>
public static class SeedData
{
    public static void Initialize(DemoContext db)
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        // The Shawshank Redemption
        var shawshank = new Movie
        {
            Name = "The Shawshank Redemption",
            Genre = Genre.Drama,
            Released = new DateTime(1994, 10, 14),
            Rating = 9.3,
            CreatedBy = 1,
            Director = new Person
            {
                FirstName = "Frank",
                LastName = "Darabont",
                Dob = new DateTime(1959, 1, 28),
            },
        };
        shawshank.Actors =
        [
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1958, 10, 16),
                    FirstName = "Tim",
                    LastName = "Robbins",
                },
                Movie = shawshank,
            },
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1937, 9, 21),
                    FirstName = "Morgan",
                    LastName = "Freeman",
                },
                Movie = shawshank,
            },
        ];

        db.Movies.Add(shawshank);

        // The Godfather
        var francis = new Person
        {
            Dob = new DateTime(1939, 4, 7),
            FirstName = "Francis",
            LastName = "Coppola",
        };
        var godfather = new Movie
        {
            Name = "The Godfather",
            Genre = Genre.Drama,
            Released = new DateTime(1972, 3, 24),
            Rating = 9.2,
            Director = francis,
            CreatedBy = 1,
        };
        godfather.Actors =
        [
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1924, 4, 3),
                    Died = new DateTime(2004, 7, 1),
                    FirstName = "Marlon",
                    LastName = "Brando",
                },
                Movie = godfather,
            },
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1940, 4, 25),
                    FirstName = "Al",
                    LastName = "Pacino",
                },
                Movie = godfather,
            },
        ];
        godfather.Writers = [new Writer { Person = francis, Movie = godfather }];

        db.Movies.Add(godfather);

        // The Dark Knight
        var nolan = new Person
        {
            Dob = new DateTime(1970, 7, 30),
            FirstName = "Christopher",
            LastName = "Nolan",
        };
        var darkKnight = new Movie
        {
            Name = "The Dark Knight",
            Genre = Genre.Action,
            Released = new DateTime(2008, 7, 18),
            Rating = 9.0,
            Director = nolan,
            CreatedBy = 2,
        };
        darkKnight.Actors =
        [
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1974, 1, 30),
                    FirstName = "Christian",
                    LastName = "Bale",
                },
                Movie = darkKnight,
            },
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1979, 4, 4),
                    Died = new DateTime(2008, 1, 22),
                    FirstName = "Heath",
                    LastName = "Ledger",
                },
                Movie = darkKnight,
            },
        ];

        db.Movies.Add(darkKnight);

        // Inception
        var inception = new Movie
        {
            Name = "Inception",
            Genre = Genre.Scifi,
            Released = new DateTime(2010, 7, 16),
            Rating = 8.8,
            Director = nolan,
            CreatedBy = 2,
        };
        inception.Actors =
        [
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1974, 11, 11),
                    FirstName = "Leonardo",
                    LastName = "DiCaprio",
                },
                Movie = inception,
            },
        ];

        db.Movies.Add(inception);

        // Pulp Fiction
        var tarantino = new Person
        {
            Dob = new DateTime(1963, 3, 27),
            FirstName = "Quentin",
            LastName = "Tarantino",
        };
        var pulpFiction = new Movie
        {
            Name = "Pulp Fiction",
            Genre = Genre.Drama,
            Released = new DateTime(1994, 10, 14),
            Rating = 8.9,
            Director = tarantino,
            CreatedBy = 1,
        };
        pulpFiction.Actors =
        [
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1954, 2, 18),
                    FirstName = "John",
                    LastName = "Travolta",
                },
                Movie = pulpFiction,
            },
            new Actor
            {
                Person = new Person
                {
                    Dob = new DateTime(1948, 12, 21),
                    FirstName = "Samuel",
                    LastName = "Jackson",
                },
                Movie = pulpFiction,
            },
        ];
        pulpFiction.Writers = [new Writer { Person = tarantino, Movie = pulpFiction }];

        db.Movies.Add(pulpFiction);

        db.SaveChanges();
    }
}
