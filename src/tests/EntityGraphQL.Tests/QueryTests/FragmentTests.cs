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
            var qr = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            dynamic person = Enumerable.First((dynamic)qr.Data["people"]);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("id"));
            Assert.NotNull(person.GetType().GetField("name"));
            Assert.NotNull(person.GetType().GetField("projects"));
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
            var qr = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            dynamic person = Enumerable.First((dynamic)qr.Data["people"]);
            // we only have the fields requested
            Assert.Equal(1, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("projects"));
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

            var res = schema.ExecuteRequestWithContext(gql, context, null, null);
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

            var res = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestIntrospectionDoubleFragment()
        {
            var schema = new SchemaProvider<TestDataContext>();
            schema.AddType<Person>("Person", "Person details", type =>
            {
                type.AddField("id", p => p.Id, "ID");
            });

            var gql = new QueryRequest
            {
                Query = @"query IntrospectionQuery {
                    __type(name: ""Person"") {
                        ...FullType
                    }
                }

                fragment FullType on __Type {
                    name
                    fields(includeDeprecated: true) {
                        name
                        type {
                            ...TypeRef
                        }
                    }
                }

                fragment TypeRef on __Type {
                    name
                    kind
                    ofType {
                        name
                        kind
                    }
                }"
            };

            var context = new TestDataContext();

            var res = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(res.Errors);
            dynamic typeData = res.Data["__type"];
            Assert.Equal("Person", typeData.name);
            Assert.Single(typeData.fields);
            var field = typeData.fields[0];
            Assert.Equal("id", field.name);
            Assert.Equal("NON_NULL", field.type.kind);
            Assert.Equal("Int", field.type.ofType.name);
            Assert.Equal("SCALAR", field.type.ofType.kind);
        }
    }
}