using System;
using System.Collections.Generic;

namespace Benchmarks
{
    public class Person
    {
        public Person(Guid id, string firstName, string lastName, DateTime dob, IEnumerable<Movie> directorOf)
        {
            Id = id;
            FirstName = firstName;
            LastName = lastName;
            Dob = dob;
            DirectorOf = directorOf;
        }

        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public IEnumerable<Movie> DirectorOf { get; set; }
    }
}