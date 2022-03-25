using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using System.Collections.Generic;

namespace EntityGraphQL.Tests
{
    public class FragmentTests
    {

        [Fact]
        public void SupportsFragmentSelectionSyntax()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schemaProvider).Compile(@"
query {
    people { ...info projects { id name } }
}
fragment info on Person {
    id name
}
");

            Assert.Single(tree.Operations.First().QueryFields);
            var qr = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
            dynamic person = Enumerable.First((dynamic)qr.Data["people"]);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Equal("id", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
            Assert.Equal("projects", person.GetType().GetFields()[2].Name);
        }
        [Fact]
        public void SupportsFragmentWithDirective()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schemaProvider).Compile(@"
query {
    people {
        ...info @skip(if: true)
        projects { id name } }
}
fragment info on Person {
    id name
}
");

            Assert.Single(tree.Operations.First().QueryFields);
            var qr = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null);
            dynamic person = Enumerable.First((dynamic)qr.Data["people"]);
            // we only have the fields requested
            Assert.Equal(1, person.GetType().GetFields().Length);
            Assert.Equal("projects", person.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void TestReuseFragment()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Query().AddField("activeProjects",
                ctx => ctx.Projects, // pretent you id some filtering here
                "Active projects").IsNullable(false);
            schema.Query().AddField("oldProjects",
                ctx => ctx.Projects, // pretent you id some filtering here
                "Old projects").IsNullable(false);

            var gql = new QueryRequest
            {
                Query = @"query {
  activeProjects {
    ...frag
  }
  oldProjects {
    ...frag
  }
}

fragment frag on Project {
  id
}"
            };

            var context = new TestDataContext().FillWithTestData();

            var res = schema.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestFragmentWithFieldThatSkipsARelation()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.UpdateType<Project>(projectType =>
            {
                projectType.AddField("manager", p => p.Owner.Manager, "The manager of the owner");
            });

            var gql = new QueryRequest
            {
                Query = @"query {
  projects {
      ...frag
  }
}

fragment frag on Project {
  manager {
      name
  }
}"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 9,
                        Owner = new Person
                        {
                            Name = "Bill",
                            Manager = new Person
                            {
                                Name = "Jill"
                            }
                        },
                        Tasks = new List<Task> { new Task() }
                    },
                },
            };

            var res = schema.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);
        }
    }
}