using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityQueryLanguage.GraphQL.Parsing;
using Microsoft.EntityFrameworkCore;
using static EntityQueryLanguage.ArgumentHelper;
using EntityQueryLanguage.Schema;
using EntityQueryLanguage.Compiler;
using System;

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
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(1, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void SupportsArguments()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            // Add a argument field with a require parameter
            schemaProvider.AddField("user", new {id = Required<int>()}, (ctx, param) => ctx.Users.FirstOrDefault(u => u.Id == param.id), "Return a user by ID");
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
	user(id: 1) { id }
}");

            Assert.Single(tree.Fields);
            dynamic user = tree.Fields.ElementAt(0).Execute(new TestSchema());
            // we only have the fields requested
            Assert.Equal(1, user.GetType().GetFields().Length);
            Assert.Equal("Id", user.GetType().GetFields()[0].Name);
            Assert.Equal(1, user.Id);
        }

        [Fact]
        public void ThrowsOnMissingRequiredArgument()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            // Add a argument field with a require parameter
            schemaProvider.AddField("user", new {id = Required<int>()}, (ctx, param) => ctx.Users.FirstOrDefault(u => u.Id == param.id), "Return a user by ID");
            var ex = Assert.Throws<SchemaException>(() => new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
                user { id }
            }"));
            Assert.Equal("Error compiling field or query 'user'. Missing required argument 'id' for field 'user'", ex.Message);
        }

        [Fact]
        public void SupportsArgumentsDefaultValue()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
            // Add a argument field with a default parameter
            schemaProvider.AddField("me", new {id = 9}, (ctx, param) => ctx.Users.FirstOrDefault(u => u.Id == param.id), "Return me, or someone else");
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
                me { id }
            }");

            Assert.Single(tree.Fields);
            dynamic user = tree.Fields.ElementAt(0).Execute(new TestSchema());
            // we only have the fields requested
            Assert.Equal(1, user.GetType().GetFields().Length);
            Assert.Equal("Id", user.GetType().GetFields()[0].Name);
            Assert.Equal(9, user.Id);
        }

        [Fact]
        public void SupportsDefaultArgumentsInNonRoot()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
            schemaProvider.Type<Person>().ReplaceField("height", new {unit = HeightUnit.Cm}, (p, param) => p.GetHeight(param.unit), "Return me, or someone else");
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
                people { id, height }
            }");

            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.First(result);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("height", person.GetType().GetFields()[1].Name);
            Assert.Equal(183.0, person.height);
        }

        [Fact]
        public void SupportsArgumentsInNonRoot()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
            schemaProvider.Type<Person>().ReplaceField("height", new {unit = HeightUnit.Cm}, (p, param) => p.GetHeight(param.unit), "Return me, or someone else");
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
                people { height(unit: meter) }
            }");

            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.First(result);
            // we only have the fields requested
            Assert.Equal(1.83, person.height);
        }

        [Fact]
        public void SupportsArgumentsAuto()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
                user(id: 1) { id }
            }");

            Assert.Single(tree.Fields);
            dynamic user = tree.Fields.ElementAt(0).Execute(new TestSchema());
            // we only have the fields requested
            Assert.Equal(1, user.GetType().GetFields().Length);
            Assert.Equal("Id", user.GetType().GetFields()[0].Name);
            Assert.Equal(1, user.Id);
        }

        [Fact]
        public void SupportsArgumentsAutoWithGuid()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
                project(id: 'aaaaaaaa-bbbb-4444-1111-ccddeeff0022') { id }
            }");

            Assert.Single(tree.Fields);
            dynamic project = tree.Fields.ElementAt(0).Execute(new TestSchema());
            // we only have the fields requested
            Assert.Equal(1, project.GetType().GetFields().Length);
            Assert.Equal("Id", project.GetType().GetFields()[0].Name);
            Assert.Equal(new Guid("aaaaaaaa-bbbb-4444-1111-ccddeeff0022"), project.Id);
        }

        [Fact]
        public void SupportsArgumentsComplex()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"query {
                person(id: 'cccccccc-bbbb-4444-1111-ccddeeff0033') { id, projects { id, name } }
            }");

            Assert.Single(tree.Fields);
            dynamic user = tree.Fields.ElementAt(0).Execute(new TestSchema());
            // we only have the fields requested
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.Equal("Id", user.GetType().GetFields()[0].Name);
            Assert.Equal(new Guid("cccccccc-bbbb-4444-1111-ccddeeff0033"), user.Id);
        }
        private class TestSchema
        {
            public string Hello { get { return "returned value"; } }
            public IEnumerable<Person> People { get { return new List<Person> { new Person() }; } }
            public IEnumerable<User> Users { get { return new List<User> { new User(9), new User(1) }; } }
            public IEnumerable<Project> Projects { get { return new List<Project> { new Project() }; } }
        }

        private enum HeightUnit
        {
            Cm,
            Meter,
            Feet
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
            public Guid Id { get { return new Guid("cccccccc-bbbb-4444-1111-ccddeeff0033"); } }
            public string Name { get { return "Luke"; } }
            public string LastName { get { return "Last Name"; } }
            public User User { get { return new User(100); } }
            public double Height { get { return 183.0; } }
            public IEnumerable<Project> Projects { get { return new List<Project> { new Project() }; } }

            public double GetHeight(HeightUnit unit)
            {
                switch (unit)
                {
                    case HeightUnit.Cm: return Height;
                    case HeightUnit.Meter: return Height / 100;
                    case HeightUnit.Feet: return Height * 0.0328;
                    default: throw new NotSupportedException($"Height unit {unit} not supported");
                }
            }
        }
        private class Project
        {
            public Guid Id { get { return new Guid("aaaaaaaa-bbbb-4444-1111-ccddeeff0022"); } }
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