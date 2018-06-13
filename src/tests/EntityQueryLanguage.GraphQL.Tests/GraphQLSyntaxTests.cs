using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityQueryLanguage.GraphQL.Parsing;
using Microsoft.EntityFrameworkCore;
using static EntityQueryLanguage.ArgumentHelper;
using EntityQueryLanguage.Schema;
using EntityQueryLanguage.Compiler;

namespace EntityQueryLanguage.GraphQL.Tests
{
    public class GraphQLSyntaxTests
    {
        [Fact]
        public void SupportsQueryKeyword()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"query {
	People { id }
}");
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).AsLambda().Compile().DynamicInvoke(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(1, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void SupportsArguments()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
            // Add a argument field with a require parameter
            schemaProvider.AddField("user", new {id = Required<int>()}, (ctx, param) => ctx.Users.FirstOrDefault(u => u.Id == param.id), "Return a user by ID");
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
	user(id: 1) { id }
}");

            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).AsLambda().Compile().DynamicInvoke(new TestSchema(), 1);
            Assert.Equal(1, Enumerable.Count(result));
            var user = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(1, user.GetType().GetFields().Length);
            Assert.Equal("Id", user.GetType().GetFields()[0].Name);
            Assert.Equal(1, user.Id);
        }

        [Fact]
        public void ThrowsOnMissingRequiredArgument()
        {
        }

        [Fact]
        public void SupportsArgumentsDefaultValue()
        {
        }

        [Fact]
        public void SupportsArgumentsInNonRoot()
        {
        }

        private class TestSchema
        {
            public string Hello { get { return "returned value"; } }
            public IEnumerable<Person> People { get { return new List<Person> { new Person() }; } }
            public IEnumerable<User> Users { get { return new List<User> { new User(9), new User(1) }; } }
        }


        private class User
        {

            public User(int id)
            {
                this.Id = id;
            }

            public int Id { get; private set; }
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
            public User User { get { return new User(100); } }
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