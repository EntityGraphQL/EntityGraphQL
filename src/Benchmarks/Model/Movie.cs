using System;
using System.Collections.Generic;

namespace Benchmarks
{
    public class Movie
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public float Rating { get; set; }
        public DateTime Released { get; set; }
        public Person Director { get; set; }
        public List<Person> Actors { get; set; }
        public MovieGenre Genre { get; set; }
    }
}