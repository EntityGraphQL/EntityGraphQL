using System.Collections.Generic;
using System;

// This is a mock datamodel, what would be your real datamodel and EF context
namespace EntityQueryLanguage.Tests
{
    internal class TestDataContext
    {
        public IEnumerable<Project> Projects { get; set; }
        public IEnumerable<Task> Tasks { get; set; }
        public IEnumerable<Location> Locations { get; set; }
        public IEnumerable<Person> People { get; set; }
    }

    internal class Person
    {
        public int Id { get; set; }
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
    }

    internal class Project
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public Location Location { get; set; }
        public IEnumerable<Task> Tasks { get; set; }
        public Person Owner { get; set; }
    }

    internal class Task
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public Person Assignee { get; set; }
    }
    internal class Location
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string Planet { get; set; }
    }
}