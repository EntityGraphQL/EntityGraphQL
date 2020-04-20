using Xunit;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using EntityGraphQL.LinqQuery;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests graphql metadata
    /// </summary>
    public class MetadataTests
    {
        [Fact]
        public void Supports__typename()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
	users { __typename id }
}");

            var users = tree.ExecuteQuery(new TestSchema(), null);
            var user = Enumerable.First((dynamic)users.Data["users"]);
            // we only have the fields requested
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.Equal("__typename", user.GetType().GetFields()[0].Name);
            Assert.Equal("User", user.__typename);
        }

        private class TestSchema
        {
            public string Hello { get { return "returned value"; } }
            public IEnumerable<Person> People { get { return new List<Person> { new Person() }; } }
            public IEnumerable<User> Users { get { return new List<User> { new User() }; } }
        }

        private class DbTestSchema
        {
            public string Hello { get { return "returned value"; } }
            public DbSet<Person> People { get; }
            public DbSet<User> Users { get; }
        }


        private class User
        {
            public int Id { get { return 100; } }
            public int Field1 { get { return 2; } }
            public string Field2 { get { return "2"; } }
            public Person Relation { get { return new Person(); } }
            public Task NestedRelation { get { return new Task(); } }
        }

        private class Person
        {
            public int Id { get { return 99; } }
            public string Name { get { return "Luke"; } }
            public string LastName { get { return "Last Name"; } }
            public User User { get { return new User(); } }
            public IEnumerable<Project> Projects { get { return new List<Project> { new Project() }; } }
        }
        private class Project
        {
            public int Id { get { return 55; } }
            public string Name { get { return "Project 3"; } }
            public IEnumerable<Task> Tasks { get { return new List<Task> { new Task() }; } }
        }
        private class Task
        {
            public int Id { get { return 33; } }
            public string Name { get { return "Task 1"; } }
        }
    }
}