using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static EntityGraphQL.Tests.ServiceFieldTests;

namespace EntityGraphQL.Tests;

public class MutationArgsTests
{
    [Fact]
    public void SupportsGenericClassArg()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<Human>("HumanInput").AddAllFields();
        schema.Mutation().Add("addPerson", ([GraphQLArguments] Partial<Human> args) => 65);

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("addPerson(others: HumanInput, name: String): Int!", sdl);

        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson {
                  addPerson(name: ""Herb"" others: { age: 43 })
                }",
        };
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(res.Errors);
        Assert.Equal(65, res.Data!["addPerson"]!);
    }

    [Fact]
    public void RequiredModifierOnInputMakesArgRequired()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("addPersonReq", ([GraphQLArguments] RequiredInputArgs args) => args.Age, new SchemaBuilderOptions { AutoCreateInputTypes = true });

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("addPersonReq(name: String!, age: Int!): Int!", sdl);

        // Missing required arg should error
        var gqlMissing = new QueryRequest { Query = @"mutation AddPersonReq { addPersonReq(age: 22) }" };
        var resMissing = schema.ExecuteRequestWithContext(gqlMissing, new TestDataContext(), null, null);
        Assert.NotNull(resMissing.Errors);
        Assert.Equal("Field 'addPersonReq' - missing required argument 'name'", resMissing.Errors![0].Message);

        // Providing all args should succeed
        var gql = new QueryRequest { Query = @"mutation AddPersonReq { addPersonReq(name: ""Herb"", age: 22) }" };
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(res.Errors);
        Assert.Equal(22, res.Data!["addPersonReq"]!);
    }

    [Fact]
    public void SupportsGenericClassArgAsInputType()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("addPerson", ([GraphQLInputType] Partial<Human> args) => 65, new SchemaBuilderOptions { AutoCreateInputTypes = true });

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("addPerson(args: PartialHuman!): Int!", sdl);
        Assert.Contains("input PartialHuman {", sdl);

        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson {
                  addPerson(args: { name: ""Herb"", others: { age: 43 } })
                }",
        };
        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(res.Errors);
        Assert.Equal(65, res.Data!["addPerson"]!);
    }

    [Fact]
    public void TestMethodWithScalarComplexAndServiceArgs()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Mutation()
            .Add(
                "addPerson",
                (string token, [GraphQLArguments] InputArgs args, ConfigService service) =>
                {
                    Assert.Equal("123", token);
                    Assert.Equal("Herb", args.Name);
                    Assert.NotNull(service);
                    return args.Age;
                },
                new SchemaBuilderOptions { AutoCreateInputTypes = true }
            );

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("addPerson(token: String!, name: String!, age: Int!): Int!", sdl);

        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson {
                  addPerson( name: ""Herb"", age: 43, token: ""123"")
                }",
        };
        var serviceCollection = new ServiceCollection();
        var service = new ConfigService();
        serviceCollection.AddSingleton(service);

        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        Assert.Equal(43, res.Data!["addPerson"]!);
    }

    [Fact]
    public void TestMethodWithScalarComplexInlineAndServiceArgs()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // Note the Argument not Argument_s_
        schema
            .Mutation()
            .Add(
                "addPerson",
                (string token, [GraphQLInputType] InputArgs args, ConfigService service) =>
                {
                    Assert.Equal("123", token);
                    Assert.Equal("Herb", args.Name);
                    Assert.NotNull(service);
                    return args.Age;
                },
                new SchemaBuilderOptions { AutoCreateInputTypes = true }
            );

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("addPerson(token: String!, args: InputArgs!): Int!", sdl);
        Assert.Contains("input InputArgs {", sdl);

        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson {
                  addPerson( args: { name: ""Herb"", age: 43 }, token: ""123"")
                }",
        };
        var serviceCollection = new ServiceCollection();
        var service = new ConfigService();
        serviceCollection.AddSingleton(service);

        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        Assert.Equal(43, res.Data!["addPerson"]!);
    }

    [Fact]
    public void TestMethodWithMultipleFlattenArguments()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Mutation()
            .Add(
                "addPerson",
                ([GraphQLArguments] InputArgs args, [GraphQLArguments] InputExtraArgs extra, ConfigService service) =>
                {
                    Assert.Equal("123", extra.Token);
                    Assert.Equal("Herb", args.Name);
                    Assert.NotNull(service);
                    return args.Age;
                },
                new SchemaBuilderOptions { AutoCreateInputTypes = true }
            );

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("addPerson(name: String!, age: Int!, token: String): Int!", sdl);

        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson {
                  addPerson( name: ""Herb"", age: 43, token: ""123"")
                }",
        };
        var serviceCollection = new ServiceCollection();
        var service = new ConfigService();
        serviceCollection.AddSingleton(service);

        var res = schema.ExecuteRequestWithContext(gql, new TestDataContext(), serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        Assert.Equal(43, res.Data!["addPerson"]!);
    }
}

internal class InputArgs
{
    [GraphQLNotNull]
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

internal class InputExtraArgs
{
    public string? Token { get; set; }
}

internal class RequiredInputArgs
{
    public required string Name { get; init; }
    public required int Age { get; init; }
}

internal class Human
{
    public int Age { get; set; }
}

internal class Partial<T>
{
    public T? Others { get; set; }
    public string? Name { get; set; }
}
