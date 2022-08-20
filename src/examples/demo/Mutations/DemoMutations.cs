using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL;
using EntityGraphQL.Schema;

namespace demo.Mutations
{
    public class DemoMutations
    {
        [GraphQLMutation("Example of a mutation that takes 0 arguments")]
        public Expression<Func<DemoContext, Movie>> ExampleNoArgs(DemoContext db)
        {
            // do something smart here with db
            db.Movies.Add(new Movie());

            return ctx => ctx.Movies.First();
        }

        [GraphQLMutation("Example of a mutation that does not use the context or argments but does use registered services")]
        public int ExampleNoArgsWithService(AgeService ageService)
        {
            // we returning a scalar, you do not require the Expression<>
            // AgeService registered in DI. Use it here
            return ageService.Calc(new Person());
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
            };
            db.Movies.Add(movie);
            db.SaveChanges();
            return ctx => ctx.Movies.First(m => m.Id == movie.Id);
        }

        [GraphQLMutation]
        public Expression<Func<DemoContext, Person>> AddActor(DemoContext db, [MutationArguments] AddActorArgs args, GraphQLValidator validator)
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
        public Expression<Func<DemoContext, IEnumerable<Person>>> AddActor2(DemoContext db, [MutationArguments] AddActorArgs args)
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
                Person = person,
            };
            db.Actors.Add(actor);
            db.SaveChanges();

            return (ctx) => ctx.People.Where(p => p.FirstName == person.FirstName);
        }
        [GraphQLMutation]
        public Expression<Func<DemoContext, IEnumerable<Person>>> AddActor3(DemoContext db, [MutationArguments] AddActor3Args args)
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
    [MutationArguments]
    public class AddMovieArgs
    {
        public Genre Genre;
        [Required(AllowEmptyStrings = false, ErrorMessage = "Movie Name is required")]
        public string Name { get; set; }
        public double Rating { get; set; }
        public DateTime Released;
        public Detail Details { get; set; }
    }

    public class Detail
    {
        public string Description { get; set; }
    }

    public class AddActorArgs
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public uint MovieId { get; set; }
    }

    public class AddActor3Args
    {
        public List<string> Names { get; set; }
        public uint MovieId { get; set; }
    }
}