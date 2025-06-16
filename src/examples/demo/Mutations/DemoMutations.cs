using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL;
using EntityGraphQL.Schema;

namespace demo.Mutations;

public class DemoMutations
{
    [GraphQLMutation("Example of a mutation that takes 0 arguments")]
    public Expression<Func<DemoContext, Movie>> ExampleNoArgs(DemoContext db)
    {
        // do something smart here with db
        db.Movies.Add(
            new Movie
            {
                Name = "Example Movie",
                Director = new Person { FirstName = "Example", LastName = "Director" },
            }
        );

        return ctx => ctx.Movies.First();
    }

    [GraphQLMutation("Example of a mutation that does not use the context or arguments but does use registered services")]
    public int ExampleNoArgsWithService(AgeService ageService)
    {
        // we returning a scalar, you do not require the Expression<>
        // AgeService registered in DI. Use it here
        return ageService.Calc(new Person { FirstName = "Example", LastName = "Bob" }.Dob);
    }

    /// <summary>
    /// Mutation methods must be marked with the [GraphQLMutation] attribute.
    ///
    /// This mutation can be used like
    /// mutation MyMutation($genre: String!, $name: String!, $released: String!) {
    ///     addMovie(genre: $genre, name: $name, released: $released) {
    ///         id name
    ///     }
    /// }
    /// </summary>
    /// <param name="db">The first parameter must be the Schema context. This lets you operate on that context. In this case the EF DB Context</param>
    /// <param name="args">The second parameter is a class that has public fields or properties matching the argument names you want to use in the mutation</param>
    /// <returns></returns>
    [GraphQLMutation("Add a new Movie object")]
    public Expression<Func<DemoContext, Movie>> AddMovie(DemoContext db, AddMovieArgs args)
    {
        var movie = new Movie
        {
            Genre = args.Genre,
            Name = args.Name,
            Released = args.Released,
            Rating = args.Rating,
            Director = new Person { FirstName = "Example", LastName = "Director" },
        };
        db.Movies.Add(movie);
        db.SaveChanges();
        return ctx => ctx.Movies.First(m => m.Id == movie.Id);
    }

    [GraphQLMutation("Add a new Movie object")]
    public Expression<Func<DemoContext, Movie>>? UpdateMovie(DemoContext db, UpdateMovieArgs args, IGraphQLValidator validator)
    {
        var movie = db.Movies.FirstOrDefault(x => x.Id == args.Id);
        if (movie is null)
        {
            validator.AddError("Movie not found");
            return null;
        }

        // Cannot be null if not set
        if (args.IsSet(nameof(args.DirectorId)))
            movie.DirectorId = args.DirectorId;

        if (!string.IsNullOrEmpty(args.Name))
            movie.Name = args.Name;

        if (args.Genre.HasValue)
            movie.Genre = args.Genre.Value;

        if (args.Released.HasValue)
            movie.Released = args.Released.Value;

        if (args.Rating.HasValue)
            movie.Rating = args.Rating.Value;

        db.Movies.Update(movie);
        db.SaveChanges();
        return ctx => ctx.Movies.First(m => m.Id == movie.Id);
    }

    [GraphQLMutation]
    public Expression<Func<DemoContext, Person>>? AddActor(DemoContext db, [GraphQLArguments] AddActorArgs args, IGraphQLValidator validator)
    {
        if (string.IsNullOrEmpty(args.FirstName))
            validator.AddError("Name argument is required");
        if (db.Movies.FirstOrDefault(m => m.Id == args.MovieId) == null)
            validator.AddError("MovieId not found");
        // ... do more validation

        if (validator.HasErrors)
            return null;

        //  we're here and valid
        var person = new Person
        {
            Id = (uint)new Random().Next(),
            FirstName = args.FirstName,
            LastName = args.LastName,
        };
        db.People.Add(person);
        var actor = new Actor
        {
            MovieId = args.MovieId,
            Movie = new Movie { Name = "Movie 3", Director = person },
            Person = person,
        };
        db.Actors.Add(actor);
        db.SaveChanges();

        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }

    /// <summary>
    /// Poor example showing you how to return a list of items as a result
    /// </summary>
    /// <param name="db"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    [GraphQLMutation]
    public Expression<Func<DemoContext, IEnumerable<Person>>> AddActor2(DemoContext db, [GraphQLArguments] AddActorArgs args)
    {
        var person = new Person
        {
            Id = (uint)new Random().Next(),
            FirstName = args.FirstName,
            LastName = args.LastName,
        };
        db.People.Add(person);
        var actor = new Actor
        {
            MovieId = args.MovieId,
            Movie = new Movie { Name = "Movie 4", Director = person },
            Person = person,
        };
        db.Actors.Add(actor);
        db.SaveChanges();

        return (ctx) => ctx.People.Where(p => p.FirstName == person.FirstName);
    }

    [GraphQLMutation]
    public Expression<Func<DemoContext, IEnumerable<Person>>> AddActor3(DemoContext db, [GraphQLArguments] AddActor3Args args)
    {
        var person = new Person
        {
            Id = (uint)new Random().Next(),
            FirstName = args.Names.First(),
            LastName = args.Names.Last(),
        };
        db.People.Add(person);
        var actor = new Actor
        {
            MovieId = args.MovieId,
            Movie = new Movie { Name = "Movie 4", Director = person },
            Person = person,
        };
        db.Actors.Add(actor);
        db.SaveChanges();

        return (ctx) => ctx.People.Where(p => p.FirstName == person.FirstName);
    }
}

/// <summary>
/// Must be a public class. Public fields and Properties are the mutation's arguments
/// </summary>
[GraphQLArguments]
public class AddMovieArgs
{
    public Genre Genre;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Movie Name is required")]
    public required string Name { get; set; }
    public double Rating { get; set; }
    public DateTime Released;
}

public class Detail
{
    public required string Description { get; set; }
}

[GraphQLArguments]
public class UpdateMovieArgs : PropertySetTrackingDto
{
    public required long Id { get; set; }
    public Genre? Genre;
    public string? Name { get; set; }
    public double? Rating { get; set; }
    public DateTime? Released;
    public uint? DirectorId { get; set; }
}

public class AddActorArgs
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public uint MovieId { get; set; }
}

public class AddActor3Args
{
    public required List<string> Names { get; set; }
    public uint MovieId { get; set; }
}
