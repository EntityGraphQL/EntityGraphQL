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
    /// Tests the extended (non-GraphQL - came first) LINQ style querying functionality
    /// </summary>
    public class CompilerTests
    {
        [Fact]
        public void ExpectsOpenBrace()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
	myEntity { field1 field2 }
}"));
            Assert.Equal("Error: line 2:19 no viable alternative at input 'field2'", ex.Message);
        }

        [Fact]
        public void ExpectsOpenBraceForEntity()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@" {
	myEntity field1 field2 }
}"));
            Assert.Equal("Error: line 2:10 no viable alternative at input 'field1'", ex.Message);
        }

        [Fact]
        public void ExpectsCloseBraceForEntity()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@" {
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
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().Fields);
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Single(result.Data);
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void CanParseSimpleQueryOptionalComma()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestSchema>();
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"
{
	people { id, name }
}");
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().Fields);
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Single(result.Data);
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
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
            Assert.Single(tree.Operations);
            Assert.Equal(2, tree.Operations.First().Fields.Count());
            var result = tree.Operations.First().Fields.ElementAt(1).Execute(new TestSchema()) as int?;
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
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().Fields);
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Single(result.Data);
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("thing", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void CanRemoveFields()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            schema.Type<Person>().RemoveField(p => p.Id);
            var ex = Assert.Throws<SchemaException>(() => { new GraphQLCompiler(schema, new DefaultMethodProvider()).Compile(@"
{
	people { id }
}");});
            Assert.Equal("Error compiling query 'people'. Field 'id' not found on current context 'Person'", ex.Message);
        }

        [Fact]
        public void CanParseSimpleQuery2()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people.where(id = 9) { id name }
}");
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().Fields);
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Empty((dynamic)result.Data["people"]);
        }
        [Fact]
        public void CanParseAliasQuery()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	luke: people.where(id = 99) { id name }
}");
            Assert.Equal("luke", tree.Operations.First().Fields.ElementAt(0).Name);
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Single((dynamic)result.Data["luke"]);
        }
        [Fact]
        public void CanParseAliasQueryComplexExpression()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id fullName: name + "" "" + lastName }
}");
            Assert.Single(tree.Operations.First().Fields);
            Assert.Equal("people", tree.Operations.First().Fields.ElementAt(0).Name);
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Single(result.Data);
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("fullName", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void FailsBinaryAsQuery()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people.id = 9 { id name }
}"));
            Assert.Equal("Error: line 3:11 extraneous input '=' expecting 38", ex.Message);
        }

        [Fact]
        public void CanParseMultipleEntityQuery()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name },
	users { id }
}");

            Assert.Single(tree.Operations);
            Assert.Equal(2, tree.Operations.First().Fields.Count());
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);

            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["users"]));
            var user = Enumerable.ElementAt((dynamic)result.Data["users"], 0);
            // we only have the fields requested
            Assert.Single(user.GetType().GetFields());
            Assert.Equal("id", user.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void CanParseQueryWithRelation()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name user { field1 } }
}");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("user", person.GetType().GetFields()[2].Name);
            var user = person.user;
            Assert.Single(user.GetType().GetFields());
            Assert.Equal("field1", user.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void CanParseQueryWithRelationDeep()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name
		user {
			field1
			nestedRelation { id name }
		}
	}
}");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1, NestedRelation = new { Id = p.User.NestedRelation.Id, Name = p.User.NestedRelation.Name } })
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("user", person.GetType().GetFields()[2].Name);
            var user = person.user;
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.Equal("field1", user.GetType().GetFields()[0].Name);
            Assert.Equal("nestedRelation", user.GetType().GetFields()[1].Name);
            var nested = person.user.nestedRelation;
            Assert.Equal(2, nested.GetType().GetFields().Length);
            Assert.Equal("id", nested.GetType().GetFields()[0].Name);
            Assert.Equal("name", nested.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void CanParseQueryWithCollection()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name projects { name } }
}");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("projects", person.GetType().GetFields()[2].Name);
            var projects = person.projects;
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.ElementAt(projects, 0);
            Assert.Equal(1, project.GetType().GetFields().Length);
            Assert.Equal("name", project.GetType().GetFields()[0].Name);
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
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            // make sure we sub-select correctly to make the requested object graph
            Assert.Equal("projects", person.GetType().GetFields()[1].Name);
            var projects = person.projects;
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.ElementAt(projects, 0);
            Assert.Equal(2, project.GetType().GetFields().Length);
            Assert.Equal("name", project.GetType().GetFields()[0].Name);
            Assert.Equal("tasks", project.GetType().GetFields()[1].Name);

            var tasks = project.tasks;
            Assert.Equal(1, Enumerable.Count(tasks));
            var task = Enumerable.ElementAt(tasks, 0);
            Assert.Equal(2, task.GetType().GetFields().Length);
            Assert.Equal("id", task.GetType().GetFields()[0].Name);
            Assert.Equal("name", task.GetType().GetFields()[1].Name);
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
            Assert.Equal("Error compiling query 'blahs'. Field 'blahs' not found on current context 'Project'", ex.Message);
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
            Assert.Equal("Error compiling query 'projects'. Field 'name3' not found on current context 'Project'", ex.Message);
        }

        [Fact]
        public void CanExecuteRequiredParameter()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider()).Compile(@"
{
	project(id: 55) {
		name
	}
}");

            Assert.Single(tree.Operations.First().Fields);
            var result = tree.ExecuteQuery(new TestSchema());
            Assert.Equal("Project 3", ((dynamic)result.Data["project"]).name);
        }

        private class TestSchema
        {
            public string Hello { get { return "returned value"; } }
            public IEnumerable<Person> People { get { return new List<Person> { new Person() }; } }
            public IEnumerable<User> Users { get { return new List<User> { new User() }; } }
            public IEnumerable<Project> Projects { get { return new List<Project> { new Project() }; } }
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
            public uint Id { get { return 55; } }
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