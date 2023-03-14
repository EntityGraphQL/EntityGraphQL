using System;
using System.Collections.Generic;
using System.Linq;

namespace Benchmarks
{
    public class DataLoader
    {
        public static void EnsureDbCreated(BenchmarkContext db)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            // db.Database.EnsureDeleted();
            // db.Database.EnsureCreated();
            // var movieData = JsonConvert.DeserializeObject<List<MovieData>>(File.ReadAllText("./DataLoader/moviedata.json"));
            // foreach (var movie in movieData.OrderByDescending(m => m.Info.Rating).Take(1000))
            // {
            //     db.Movies.Add(new Movie
            //     {
            //         Id = Guid.NewGuid(),
            //         Name = movie.Title,
            //         Released = movie.Info.ReleaseDate,
            //         Rating = movie.Info.Rating,
            //         Genre = movie.Info.Genres != null ? GetOrMakeGenre(db, movie.Info.Genres[0]) : null,
            //         Actors = MakePersons(db, movie.Info.Actors),
            //         Director = MakePerson(db, movie.Info.Directors?.FirstOrDefault()),
            //     });
            //     db.SaveChanges();
            // }
            // var movies = db.Movies.OrderByDescending(m => m.Rating).Take(10).ToList();
            // var top = movies.FirstOrDefault();
        }

        private static MovieGenre GetOrMakeGenre(BenchmarkContext db, string name)
        {
            var g = db.Genres.FirstOrDefault(i => i.Name == name);
            if (g == null)
            {
                g = new MovieGenre(name);
                db.Genres.Add(g);
            }
            return g;
        }

        private static List<Person>? MakePersons(BenchmarkContext db, List<string> actors)
        {
            if (actors == null)
                return null;
            var actorData = actors.Select(p => MakePerson(db, p));
            return actorData.Where(p => p != null).ToList()!;
        }

        private static Person? MakePerson(BenchmarkContext db, string fullName)
        {
            if (fullName == null)
                return null;

            string[] split = fullName.Split(' ');
            string fName = split[0];
            string lName = split.Length > 1 ? split[1] : "";
            var person = db.People.FirstOrDefault(p => p.FirstName == fName && p.LastName == lName);
            if (person == null)
            {
                person = new Person(Guid.NewGuid(), fName, lName, new DateTime(1957, 2, 2), new List<Movie>());
                db.People.Add(person);
            }
            return person;
        }
    }
}