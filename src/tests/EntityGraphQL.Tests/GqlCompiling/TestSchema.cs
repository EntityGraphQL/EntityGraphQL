using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EntityGraphQL.Tests.GqlCompiling
{
    internal class TestSchema
    {
        public string Hello { get { return "returned value"; } }
        public IEnumerable<Person> People { get { return new List<Person> { new Person() }; } }
        public IEnumerable<User> Users { get { return new List<User> { new User() }; } }
        public IEnumerable<Project> Projects { get { return new List<Project> { new Project() }; } }
    }
    internal class DbTestSchema
    {
        public string Hello { get { return "returned value"; } }
        public DbSet<Person> People { get; }
        public DbSet<User> Users { get; }
    }


    internal class User
    {
        public int Id { get { return 100; } }
        public int Field1 { get { return 2; } }
        public string Field2 { get { return "2"; } }
        public Person Relation { get { return new Person(); } }
        public Task NestedRelation { get { return new Task(); } }
    }

    internal class Person
    {
        public int Id { get { return 99; } }
        public string Name { get { return "Luke"; } }
        public string LastName { get { return "Last Name"; } }
        public DateTime Birthday { get { return new DateTime(2000, 1, 1, 1, 1,1, 1); } }
        public User User { get { return new User(); } }
        public IEnumerable<Project> Projects { get { return new List<Project> { new Project() }; } }
    }
    internal class Project
    {
        public uint Id { get { return 55; } }
        public string Name { get { return "Project 3"; } }
        public IEnumerable<Task> Tasks { get { return new List<Task> { new Task() }; } }
    }
    internal class Task
    {
        public int Id { get { return 33; } }
        public string Name { get { return "Task 1"; } }
    }
}