using Xunit;
using EntityGraphQL.Schema;
using System.Collections.Generic;
using EntityGraphQL.Directives;
using EntityGraphQL.Compiler;
using EntityGraphQL.Tests.SubscriptionTests;

namespace EntityGraphQL.Tests;

public class DirectiveOnVisitTests
{
    [Fact]
    public void TestOnVisitListFieldRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                people @myDirective {
                    id
                }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitObjectFieldRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                person(id: 1) @myDirective {
                    id
                }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitScalarFieldRoot()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                totalPeople @myDirective
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }

    [Fact]
    public void TestOnVisitListField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                people {
                    id
                    projects @myDirective {
                        id
                    }
                }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitObjectField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                people {
                    id
                    manager @myDirective {
                        id
                    }
                }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitScalarField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                people {
                    id
                    name @myDirective
                }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }

    [Fact]
    public void TestOnVisitFragmentDef()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FRAGMENT_DEFINITION);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                people {
                    id
                }
            }
            fragment myFragment on Person @myDirective {
                id
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FRAGMENT_DEFINITION, directive.WasVisited);
    }

    [Fact]
    public void TestOnVisitFragmentSpread()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FRAGMENT_SPREAD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                people {
                    ...myFragment @myDirective
                }
            }
            fragment myFragment on Person {
                id
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FRAGMENT_SPREAD, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitInlineFragment()
    {
        var schema = SchemaBuilder.FromObject<TestUnionDataContext>(new SchemaBuilderOptions { AutoCreateInterfaceTypes = true });
        var directive = new MyDirecitive(ExecutableDirectiveLocation.INLINE_FRAGMENT);
        schema.AddDirective(directive);

        schema.Type<IAnimal>().AddPossibleType<Dog>();
        schema.Type<IAnimal>().AddPossibleType<Cat>();

        var gql = new GraphQLCompiler(schema).Compile(@"query {
            animals {
                __typename
                ... on Dog @myDirective {
                    name
                    hasBone
                }
                ... on Cat {
                    name
                    lives
                }
            }
        }");
        var context = new TestUnionDataContext();
        context.Animals.Add(new Dog() { Name = "steve", HasBone = true });
        context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

        gql.ExecuteQuery(context, null, null);
        Assert.Equal(ExecutableDirectiveLocation.INLINE_FRAGMENT, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitMutationField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        schema.Mutation().Add("doStuff", () =>
        {
            return new Person() { Id = 1, Name = "bob" };
        });
        var query = new QueryRequest
        {
            Query = @"mutation {
                doStuff @myDirective {
                    id
                }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }

    [Fact]
    public void TestOnVisitMutationInnerField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        schema.Mutation().Add("doStuff", () =>
        {
            return new Person() { Id = 1, Name = "bob" };
        });
        var query = new QueryRequest
        {
            Query = @"mutation {
                doStuff {
                    id @myDirective
                }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }

    [Fact]
    public void TestOnVisitMutationStatement()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.MUTATION);
        schema.AddDirective(directive);
        schema.Mutation().Add("doStuff", () =>
        {
            return new Person() { Id = 1, Name = "bob" };
        });
        var query = new QueryRequest
        {
            Query = @"mutation @myDirective {
                doStuff {
                    id
                }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.MUTATION, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitQueryStatement()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.QUERY);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query @myDirective {
                people { id }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.QUERY, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitSubscriptionStatement()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        schema.Subscription().AddFrom<TestSubscriptions>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.SUBSCRIPTION);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"subscription @myDirective {
                onMessage { id }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.SUBSCRIPTION, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitSubscriptionField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Message>("Message info").AddAllFields();
        schema.Subscription().AddFrom<TestSubscriptions>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FIELD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"subscription {
                onMessage @myDirective { id }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FIELD, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitVariableDef()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.VARIABLE_DEFINITION);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query Q($id: Int @myDirective) {
                people { id }
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.VARIABLE_DEFINITION, directive.WasVisited);
    }
    [Fact]
    public void TestOnVisitCalledOnce()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var directive = new MyDirecitive(ExecutableDirectiveLocation.FRAGMENT_SPREAD);
        schema.AddDirective(directive);
        var query = new QueryRequest
        {
            Query = @"query {
                people {
                    id
                    ...myFragment @myDirective
                }
            }
            fragment myFragment on Person {
                id
            }"
        };
        schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
        Assert.Equal(ExecutableDirectiveLocation.FRAGMENT_SPREAD, directive.WasVisited);
        Assert.Equal(1, directive.Calls);
    }
}

internal class MyDirecitive : DirectiveProcessor<object>
{
    private readonly ExecutableDirectiveLocation location;

    public override string Name { get => "myDirective"; }
    public override string Description { get => "My directive"; }
    public override List<ExecutableDirectiveLocation> Location => new() {
        location
    };

    public ExecutableDirectiveLocation? WasVisited { get; private set; }
    public int Calls { get; private set; }

    public MyDirecitive(ExecutableDirectiveLocation location)
    {
        this.location = location;
    }

    public override IGraphQLNode VisitNode(ExecutableDirectiveLocation location, IGraphQLNode node, object arguments)
    {
        WasVisited = location;
        Calls += 1;
        return node;
    }
}