using System;
using System.Collections.Generic;

namespace Benchmarks
{
    public class Movie
    {
        public Movie(Guid id, string name, float rating, DateTime released, Person director, List<Person> actors, MovieGenre genre)
        {
            Id = id;
            Name = name;
            Rating = rating;
            Released = released;
            Director = director;
            Actors = actors;
            Genre = genre;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public float Rating { get; set; }
        public DateTime Released { get; set; }
        public Person Director { get; set; }
        public List<Person> Actors { get; set; }
        public MovieGenre Genre { get; set; }
    }
}