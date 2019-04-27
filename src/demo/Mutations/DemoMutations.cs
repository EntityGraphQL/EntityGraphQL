using System;
using EntityGraphQL.Schema;

namespace demo.Mutations
{
    public class DemoMutations
    {
        public DemoMutations()
        {
        }

        /// <summary>
        /// Mutation methods must be marked with the GraphQLMutation attribute.
        ///
        /// This mutation can be used as
        /// mutation MyMutation($genre: String!, $name: String!, $released: String!) {
        ///     addMovie(genre: $genre, name: $name, released: $released) {
        ///         id name
        ///     }
        /// }
        /// </summary>
        /// <param name="db">The first parameter must be the Schema context</param>
        /// <param name="args">The second parameter is a class that has public fields or properties matching the argument names you want to use in the mutation</param>
        /// <returns></returns>
        [GraphQLMutation]
        public Movie AddMovie(DemoContext db, AddMovieArgs args)
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
            return movie;
        }

        [GraphQLMutation]
        public Person AddActor(DemoContext db, AddActorArgs args)
        {
            var person = new Person {
                FirstName = args.FirstName,
                LastName = args.LastName,
            };
            db.People.Add(person);
            var actor = new Actor {
                MovieId = args.MovieId,
                PersonId = person.Id,
            };
            db.Actors.Add(actor);
            db.SaveChanges();
            return person;
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