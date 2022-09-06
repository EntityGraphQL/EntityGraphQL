using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using System.Collections.Generic;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests for edge cases on collection/list fields
    /// </summary>
    public class ListEdgeCasesTests
    {
        [Fact]
        public void WildcardQueriesHonorRemovedFieldsOnList()
        {
            // empty schema
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<Person>().RemoveField(p => p.Id);
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(schema).Compile(@"
            {
                people
            }"));
            Assert.Equal("Field 'people' requires a selection set defining the fields you would like to select.", ex.Message);
        }

        [Fact]
        public void WildcardQueriesHonorRemovedFieldsOnListFromEmpty()
        {
            // empty schema
            var schema = SchemaBuilder.Create<TestDataContext>();
            schema.AddType<Person>("Person").AddField("name", p => p.Name, "Person's name");
            schema.Query().AddField("people", p => p.People, "People");
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(schema).Compile(@"
            {
                people
            }"));
            Assert.Equal("Field 'people' requires a selection set defining the fields you would like to select.", ex.Message);
        }

        [Fact]
        public void CanParseQueryWithCollection()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
        {
        	people { id name projects { name } }
        }");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "id");
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "name");
            // make sure we sub-select correctly to make the requested object graph
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "projects");
            var projects = person.projects;
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.ElementAt(projects, 0);
            Assert.Equal(1, project.GetType().GetFields().Length);
            Assert.NotNull(project.GetType().GetField("name"));
        }

        [Fact]
        public void CanParseQueryWithCollectionDeep()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
        {
        	people { id
        		projects {
        			name
        			tasks { id name }
        		}
        	}
        }");
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "id");
            // make sure we sub-select correctly to make the requested object graph
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "projects");
            var projects = person.projects;
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.ElementAt(projects, 0);
            Assert.Equal(2, project.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)project.GetType().GetFields(), f => f.Name == "name");
            Assert.Contains((IEnumerable<dynamic>)project.GetType().GetFields(), f => f.Name == "tasks");

            var tasks = project.tasks;
            Assert.Equal(4, Enumerable.Count(tasks));
            var task = Enumerable.ElementAt(tasks, 0);
            Assert.Equal(2, task.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)task.GetType().GetFields(), f => f.Name == "id");
            Assert.Contains((IEnumerable<dynamic>)task.GetType().GetFields(), f => f.Name == "name");
        }
    }
}