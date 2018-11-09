using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityQueryLanguage.GraphQL.Parsing;
using Microsoft.EntityFrameworkCore;
using EntityQueryLanguage.Schema;
using EntityQueryLanguage.Compiler;

namespace EntityQueryLanguage.GraphQL.Tests
{
    /// <summary>
    /// Tests the extended (non-GraphQL - came first) LINQ style querying functionality
    /// </summary>
    public class DataLinqCompilerTests
    {
        [Fact]
        public void ExpectsOpenBrace()
        {
            var ex = Assert.Throws<EqlCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
	myEntity { field1 field2 }
}"));
            Assert.Equal("Error: line 2:9 extraneous input ' ' expecting 6", ex.Message);
        }

        [Fact]
        public void ExpectsOpenBraceForEntity()
        {
            var ex = Assert.Throws<EqlCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@" {
	myEntity field1 field2 }
}"));
            Assert.Equal("Error: line 2:10 no viable alternative at input 'field1'", ex.Message);
        }

        [Fact]
        public void ExpectsCloseBraceForEntity()
        {
            var ex = Assert.Throws<EqlCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@" {
	myEntity {field1 field2 }"));
            Assert.Equal("Error: line 2:26 no viable alternative at input '<EOF>'", ex.Message);
        }

        [Fact]
        public void CanParseSimpleQuery()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestSchema>();
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"
{
	people { id name }
}");
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("Name", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void CanParseSimpleQueryOptionalComma()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestSchema>();
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"
{
	people { id, name }
}");
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("Name", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void CanParseScalar()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestSchema>();
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"
{
	people { id name }
    total: people.count()
}");
            Assert.Equal(2, tree.Fields.Count);
            var result = tree.Fields.ElementAt(1).Execute(new TestSchema()) as int?;
            Assert.True(result.HasValue);
            Assert.Equal(1, result.Value);
        }
        [Fact]
        public void CanQueryExtendedFields()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestSchema>();
            objectSchemaProvider.Type<Person>().AddField("thing", p => p.Id + " - " + p.Name, "A weird field I want");
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"
{
	people { id thing }
}");
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("thing", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void CanRemoveFields()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            schema.Type<Person>().RemoveField(p => p.Id);
            var ex = Assert.Throws<SchemaException>(() => { var tree = new GraphQLCompiler(schema, new DefaultMethodProvider()).Compile(@"
{
	people { id }
}");});
            Assert.Equal("Error compiling field or query 'people'. Field or property 'id' not found on current context 'Person'", ex.Message);
        }

        [Fact]
        public void CanParseSimpleQuery2()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people.where(id = 9) { id name }
}");
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(0, Enumerable.Count(result));
        }
        [Fact]
        public void CanParseAliasQuery()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	luke: people.where(id = 99) { id name }
}");
            Assert.Single(tree.Fields);
            Assert.Equal("luke", tree.Fields.ElementAt(0).Name);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
        }
        [Fact]
        public void CanParseAliasQueryComplexExpression()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id fullName: name + ' ' + lastname }
}");
            Assert.Single(tree.Fields);
            Assert.Equal("people", tree.Fields.ElementAt(0).Name);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("fullName", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void FailsBinaryAsQuery()
        {
            var ex = Assert.Throws<EqlCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people.id = 9 { id name }
}"));
            Assert.Equal("Error: line 3:11 extraneous input '=' expecting 34", ex.Message);
        }

        [Fact]
        public void CanParseMultipleEntityQuery()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name },
	Users { id }
}");

            Assert.Equal(2, tree.Fields.Count);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("Name", person.GetType().GetFields()[1].Name);

            result = tree.Fields.ElementAt(1).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var user = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Single(user.GetType().GetFields());
            Assert.Equal("Id", user.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void CanParseQueryWithRelation()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name User { field1 } }
}");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("Name", person.GetType().GetFields()[1].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("User", person.GetType().GetFields()[2].Name);
            var user = person.User;
            Assert.Single(user.GetType().GetFields());
            Assert.Equal("Field1", user.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void CanParseQueryWithRelationDeep()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name
		User {
			field1
			nestedRelation { id name }
		}
	}
}");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1, NestedRelation = new { Id = p.User.NestedRelation.Id, Name = p.User.NestedRelation.Name } })
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("Name", person.GetType().GetFields()[1].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("User", person.GetType().GetFields()[2].Name);
            var user = person.User;
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.Equal("Field1", user.GetType().GetFields()[0].Name);
            Assert.Equal("NestedRelation", user.GetType().GetFields()[1].Name);
            var nested = person.User.NestedRelation;
            Assert.Equal(2, nested.GetType().GetFields().Length);
            Assert.Equal("Id", nested.GetType().GetFields()[0].Name);
            Assert.Equal("Name", nested.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void CanParseQueryWithCollection()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name projects { name } }
}");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("Name", person.GetType().GetFields()[1].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("projects", person.GetType().GetFields()[2].Name);
            var projects = person.projects;
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.ElementAt(projects, 0);
            Assert.Equal(1, project.GetType().GetFields().Length);
            Assert.Equal("Name", project.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void CanParseQueryWithCollectionDeep()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id
		projects {
			name
			tasks { id name }
		}
	}
}");
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("projects", person.GetType().GetFields()[1].Name);
            var projects = person.projects;
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.ElementAt(projects, 0);
            Assert.Equal(2, project.GetType().GetFields().Length);
            Assert.Equal("Name", project.GetType().GetFields()[0].Name);
            Assert.Equal("tasks", project.GetType().GetFields()[1].Name);

            var tasks = project.tasks;
            Assert.Equal(1, Enumerable.Count(tasks));
            var task = Enumerable.ElementAt(tasks, 0);
            Assert.Equal(2, task.GetType().GetFields().Length);
            Assert.Equal("Id", task.GetType().GetFields()[0].Name);
            Assert.Equal("Name", task.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void FailsNonExistingField()
        {
            var ex = Assert.Throws<SchemaException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id
		projects {
			name
			blahs { id name }
		}
	}
}"));
            Assert.Equal("Error compiling field or query 'blahs'. Field or property 'blahs' not found on current context 'Project'", ex.Message);
        }
        [Fact]
        public void FailsNonExistingField2()
        {
            var ex = Assert.Throws<SchemaException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id
		projects {
			name3
		}
	}
}"));
            Assert.Equal("Error compiling field or query 'projects'. Field or property 'name3' not found on current context 'Project'", ex.Message);
        }

        [Fact(Skip = "Not sure of the status")]
        public void HandlesCustomRelationHandler()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<DbTestSchema>(), new DefaultMethodProvider(), new EfRelationHandler(typeof(EntityFrameworkQueryableExtensions))).Compile(@"
{
	people { id name User { field1 } }
}");
            // People.Include(p => p.User).Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            Assert.Single(tree.Fields);
            dynamic result = tree.Fields.ElementAt(0).Execute(new TestSchema());
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.ElementAt(result, 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Equal("Id", person.GetType().GetFields()[0].Name);
            Assert.Equal("Name", person.GetType().GetFields()[1].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("User", person.GetType().GetFields()[2].Name);
            var user = person.User;
            Assert.Equal(1, user.GetType().GetFields().Length);
            Assert.Equal("Field1", user.GetType().GetFields()[0].Name);
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