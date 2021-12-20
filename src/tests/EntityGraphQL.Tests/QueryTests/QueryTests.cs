using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.EntityQuery;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests the extended (non-GraphQL - came first) LINQ style querying functionality
    /// </summary>
    public class QueryTests
    {
        [Fact]
        public void CanParseSimpleQuery()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"
{
	people { id name }
}");
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
            Assert.Single(result.Data);
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void CanQueryExtendedFields()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            objectSchemaProvider.Type<Person>().AddField("thing", p => p.Id + " - " + p.Name, "A weird field I want");
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"
{
	people { id thing }
}");
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
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
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<Person>().RemoveField(p => p.Id);
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => { var tree = new GraphQLCompiler(schema, new DefaultMethodProvider()).Compile(@"
{
	people { id }
}"); });
            Assert.Equal("Field 'id' not found on type 'Person'", ex.Message);
        }

        [Fact]
        public void CanParseMultipleEntityQuery()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name }
	users { id }
}");

            Assert.Single(tree.Operations);
            Assert.Equal(2, tree.Operations.First().QueryFields.Count());
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
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
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
{
	people { id name user { field1 } }
}");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
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
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
        {
        	people {
                id name
        		user {
        			field1
        			nestedRelation { id name }
        		}
        	}
        }");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1, NestedRelation = new { Id = p.User.NestedRelation.Id, Name = p.User.NestedRelation.Name } })
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
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
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
        {
        	people { id name projects { name } }
        }");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
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
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
        {
        	people { id
        		projects {
        			name
        			tasks { id name }
        		}
        	}
        }");
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
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
            Assert.Equal(4, Enumerable.Count(tasks));
            var task = Enumerable.ElementAt(tasks, 0);
            Assert.Equal(2, task.GetType().GetFields().Length);
            Assert.Equal("id", task.GetType().GetFields()[0].Name);
            Assert.Equal("name", task.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void FailsNonExistingField()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
        {
        	people { id
        		projects {
        			name
        			blahs { id name }
        		}
        	}
        }"));
            Assert.Equal("Field 'blahs' not found on type 'Project'", ex.Message);
        }
        [Fact]
        public void FailsNonExistingField2()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
        {
        	people { id
        		projects {
        			name3
        		}
        	}
        }"));
            Assert.Equal("Field 'name3' not found on type 'Project'", ex.Message);
        }

        [Fact]
        public void TestAlias()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
        {
        	projects {
        		n: name
        	}
        }");

            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
            Assert.Equal("Project 3", ((dynamic)result.Data["projects"])[0].n);
        }

        [Fact]
        public void TestAliasDeep()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>(), new DefaultMethodProvider()).Compile(@"
        {
        people { id
        		projects {
        			n: name
        		}
        	}
        }");

            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
            Assert.Equal("Project 3", Enumerable.First(Enumerable.First((dynamic)result.Data["people"]).projects).n);
        }
        [Fact]
        public void EnumTest()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(false);
            var gql = new QueryRequest
            {
                Query = @"{
  people {
      gender
  }
}
",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequest(gql, testSchema, null, null);
            Assert.Null(results.Errors);
        }

        [Fact]
        public void TestTopLevelScalar()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            var gql = new GraphQLCompiler(schemaProvider, new DefaultMethodProvider()).Compile(@"
query {
    totalPeople
}");

            var context = new TestDataContext();
            context.People.Clear();
            for (int i = 0; i < 15; i++)
            {
                context.People.Add(new Person());
            }
            var qr = gql.ExecuteQuery(context, null);
            dynamic totalPeople = (dynamic)qr.Data["totalPeople"];
            // we only have the fields requested
            Assert.Equal(15, totalPeople);
        }
    }
}