using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class FragmentTests
{
    [Fact]
    public void SupportsFragmentSelectionSyntax()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        // Add a argument field with a require parameter
        var tree = GraphQLParser.Parse(
            @"
                query {
                    people { ...info projects { id name } }
                }
                fragment info on Person {
                    id name
                }
            ",
            schemaProvider
        );

        Assert.Single(tree.Operations.First().QueryFields);
        var qr = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
        dynamic person = Enumerable.First((dynamic)qr.Data!["people"]!);
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
        var tree = GraphQLParser.Parse(
            @"
                query {
                    people {
                        ...info @skip(if: true)
                        projects { id name } }
                }
                fragment info on Person {
                    id name
                }
                ",
            schemaProvider
        );

        Assert.Single(tree.Operations.First().QueryFields);
        var qr = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
        dynamic person = Enumerable.First((dynamic)qr.Data!["people"]!);
        // we only have the fields requested
        Assert.Equal(1, person.GetType().GetFields().Length);
        Assert.NotNull(person.GetType().GetField("projects"));
    }

    [Fact]
    public void TestReuseFragment()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema
            .Query()
            .AddField(
                "activeProjects",
                ctx => ctx.Projects, // pretent you id some filtering here
                "Active projects"
            )
            .IsNullable(false);
        schema
            .Query()
            .AddField(
                "oldProjects",
                ctx => ctx.Projects, // pretent you id some filtering here
                "Old projects"
            )
            .IsNullable(false);

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    activeProjects {
                        ...frag
                    }
                    oldProjects {
                        ...frag
                    }
                }

                fragment frag on Project {
                    id
                }",
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
            projectType.AddField("manager", p => p.Owner!.Manager, "The manager of the owner");
        });

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    projects {
                        ...frag
                    }
                }

                fragment frag on Project {
                    manager {
                        name
                    }
                }",
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 9,
                    Owner = new Person
                    {
                        Name = "Bill",
                        Manager = new Person { Name = "Jill" },
                    },
                    Tasks = new List<Task> { new Task() },
                },
            ],
        };

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
    }

    [Fact]
    public void TestIntrospectionDoubleFragment()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<Person>(
            "Person",
            "Person details",
            type =>
            {
                type.AddField("id", p => p.Id, "ID");
            }
        );

        var gql = new QueryRequest
        {
            Query =
                @"query IntrospectionQuery {
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
                }",
        };

        var context = new TestDataContext();

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
        dynamic typeData = res.Data!["__type"]!;
        Assert.Equal("Person", typeData.name);
        Assert.Single(typeData.fields);
        var field = typeData.fields[0];
        Assert.Equal("id", field.name);
        Assert.Equal("NON_NULL", field.type.kind);
        Assert.Equal("Int", field.type.ofType.name);
        Assert.Equal("SCALAR", field.type.ofType.kind);
    }

    [Fact]
    public void Test_InlineFragment_With_Service()
    {
        var schema = SchemaBuilder.FromObject<TestUnionDataContext>(new SchemaBuilderOptions { AutoCreateInterfaceTypes = true });
        Assert.True(schema.HasType(typeof(IAnimal)));
        Assert.Equal(GqlTypes.Union, schema.GetSchemaType(typeof(IAnimal), false, null).GqlType);

        schema.Type<IAnimal>().AddPossibleType<Dog>();
        schema.Type<IAnimal>().AddPossibleType<Cat>();
        Assert.Equal(GqlTypes.QueryObject, schema.GetSchemaType(typeof(Cat), false, null).GqlType);
        Assert.Equal(GqlTypes.QueryObject, schema.GetSchemaType(typeof(Dog), false, null).GqlType);
        schema.UpdateType<Cat>(catType =>
        {
            catType.AddField("isAngry", "Is the cat angry").Resolve<CatAngerService>((cat, service) => service.IsAngry(cat.Id));
        });

        var gql = GraphQLParser.Parse(
            @"
            query {
                animals {
                    ... on Cat {
                        name
                        isAngry
                    }
                }
            }",
            schema
        );
        var context = new TestUnionDataContext();
        context.Animals.Add(new Dog() { Name = "steve", HasBone = true });
        context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

        var services = new ServiceCollection().AddSingleton<CatAngerService>();

        var qr = gql.ExecuteQuery(context, services.BuildServiceProvider(), null);
        dynamic animals = qr.Data!["animals"]!;
        // we only have the fields requested
        Assert.Equal(2, animals.Count);

        // Dogs are not null but have 0 fields
        Assert.NotNull(animals[0]);
        Assert.Empty(animals[0].GetType().GetFields());

        Assert.Equal("george", animals[1].name);
        Assert.True(animals[1].isAngry);
    }

    [Fact]
    public void TestFragmentSpreadMustNotFormCycles_DirectCycle()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        var exception = Assert.Throws<EntityGraphQLException>(() =>
            GraphQLParser.Parse(
                @"
                query {
                    people { ...PersonInfo }
                }
                fragment PersonInfo on Person {
                    name
                    ...PersonInfo
                } 
            ",
                schemaProvider
            )
        );

        Assert.Contains("Fragment spreads must not form cycles", exception.Message);
    }

    [Fact]
    public void TestFragmentSpreadMustNotFormCycles_IndirectCycle()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        var exception = Assert.Throws<EntityGraphQLException>(() =>
            GraphQLParser.Parse(
                @"
                query {
                    people { ...PersonInfo }
                }
                fragment PersonInfo on Person {
                    name
                    ...ProjectInfo
                }
                fragment ProjectInfo on Project {
                    name
                    ...PersonInfo
                }
            ",
                schemaProvider
            )
        );

        Assert.Contains("Fragment spreads must not form cycles", exception.Message);
    }

    [Fact]
    public void TestFragmentSpreadMustNotFormCycles_ComplexCycle()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        var exception = Assert.Throws<EntityGraphQLException>(() =>
            GraphQLParser.Parse(
                @"
                query {
                    people { ...A }
                }
                fragment A on Person {
                    name
                    ...B
                }
                fragment B on Person {
                    id
                    ...C
                }
                fragment C on Person {
                    lastName
                    ...A
                }
            ",
                schemaProvider
            )
        );

        Assert.Contains("Fragment spreads must not form cycles", exception.Message);
    }

    [Fact]
    public void TestFragmentSpreadMustNotFormCycles_ValidNoCycle()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        var tree = GraphQLParser.Parse(
            @"
            query {
                people { 
                    ...PersonInfo 
                    projects { ...ProjectInfo }
                }
            }
            fragment PersonInfo on Person {
                name
                id
            }
            fragment ProjectInfo on Project {
                name
                id
            }
        ",
            schemaProvider
        );

        Assert.NotNull(tree);
        var qr = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
        Assert.NotNull(qr.Data);
    }

    [Fact]
    public void TestFragmentSpreadMustNotFormCycles_ValidReusedFragment()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        var tree = GraphQLParser.Parse(
            @"
            query {
                people {
                    ...PersonBasics
                    projects {
                        ...ProjectBasics
                        owner { ...PersonBasics }
                    }
                }
            }
            fragment PersonBasics on Person {
                name
                id
            }
            fragment ProjectBasics on Project {
                name
                id
            }
        ",
            schemaProvider
        );

        Assert.NotNull(tree);
        var qr = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
        Assert.NotNull(qr.Data);
    }

    [Fact]
    public void TestFragmentWithVariables()
    {
        // Issue #483: Variables should be supported in fragments
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        var tree = GraphQLParser.Parse(
            @"
            query Projects($taskName: String) {
                projects {
                    ...ProjectFragment
                }
            }

            fragment ProjectFragment on Project {
                id
                name
                searchTasks(name: $taskName) {
                    id
                    name
                }
            }
            ",
            schemaProvider
        );

        Assert.NotNull(tree);

        var variables = new QueryVariables { { "taskName", "task 1" } };

        var context = new TestDataContext().FillWithTestData();
        var qr = tree.ExecuteQuery(context, null, variables);

        Assert.Null(qr.Errors);
        Assert.NotNull(qr.Data);

        dynamic projects = qr.Data!["projects"]!;
        Assert.Single(projects);

        dynamic project = projects[0];
        Assert.Equal(55, project.id);
        Assert.Equal("Project 3", project.name);

        // Should only get the task matching "task 1"
        Assert.Single(project.searchTasks);
        Assert.Equal("task 1", project.searchTasks[0].name);
    }
}

internal class CatAngerService
{
    // When is a cat not angry?
    internal bool IsAngry(int id) => true;
}
