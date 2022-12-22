using System;
using System.Collections.Generic;

namespace Benchmarks
{
    public class Person
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime Dob { get; set; }
        public IEnumerable<Movie> DirectorOf { get; set; }
    }
}