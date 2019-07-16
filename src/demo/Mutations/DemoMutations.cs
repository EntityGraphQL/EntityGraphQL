using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL;
using EntityGraphQL.Schema;

namespace demo.Mutations
{
    public class DemoMutations
    {
        public DemoMutations()
        {
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
        public Expression<Func<DemoContext, Person>> AddActor(DemoContext db, AddActorArgs args)
        {
            var person = new Person {
                Id = (uint)new Random().Next(),
                FirstName = args.FirstName,
                LastName = args.LastName,
            };
            db.People.Add(person);
            var actor = new Actor {
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
        public Expression<Func<DemoContext, IEnumerable<Person>>> AddActor2(DemoContext db, AddActorArgs args)
        {
            var person = new Person {
                Id = (uint)new Random().Next(),
                FirstName = args.FirstName,
                LastName = args.LastName,
            };
            db.People.Add(person);
            var actor = new Actor {
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
    public class AddMovieArgs
    {
        public Genre Genre;
        public string Name { get; set; }
        public double Rating { get; set; }
        public DateTime Released;
    }

    public class AddActorArgs
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public uint MovieId { get; set; }
    }
}