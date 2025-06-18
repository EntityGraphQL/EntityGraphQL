using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class ArgumentTrackerTests
{
    [Fact]
    public void TestPropertySetTracking_IsSet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("test", new TestArgsTracking(), (db, args) => db.People.WhereWhen(p => args.Ids!.Any(a => a == p.Guid), args.IsSet(nameof(TestArgsTracking.Ids))), "test field");
        var gql = new QueryRequest
        {
            Query =
                @"query ($ids: [ID]) {
                    test(ids: $ids) { guid }
                }",
            // assume JSON deserialiser created a List<> but we need an array []
            Variables = new QueryVariables { { "ids", new[] { "03d539f8-6bbc-4b62-8f7f-b55c7eb242e6" } } },
        };

        var testSchema = new TestDataContext();
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6") });
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e7") });
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data);
        Assert.NotNull(results.Data!["test"]);
        var testData = (dynamic)results.Data!["test"]!;
        Assert.Single(testData);
        Assert.Equal(Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6"), testData[0].guid);
    }

    [Fact]
    public void TestPropertySetTracking_NotSet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("test", new TestArgsTracking(), (db, args) => db.People.WhereWhen(p => args.Ids!.Any(a => a == p.Guid), args.IsSet(nameof(TestArgsTracking.Ids))), "test field");
        var gql = new QueryRequest
        {
            Query =
                @"query ($ids: [ID]) {
                    test(ids: $ids) { guid }
                }",
        };

        var testSchema = new TestDataContext();
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6") });
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e7") });
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data);
        Assert.NotNull(results.Data!["test"]);
        var testData = (dynamic)results.Data!["test"]!;
        Assert.Equal(2, testData.Count);
    }

    [Fact]
    public void TestPropertySetTracking_IsSet_NoVars()
    {
        // don't use variables, use inline values
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("test", new TestArgsTracking(), (db, args) => db.People.WhereWhen(p => args.Ids!.Any(a => a == p.Guid), args.IsSet(nameof(TestArgsTracking.Ids))), "test field");
        var gql = new QueryRequest
        {
            Query =
                @"query {
                    test(ids: [""03d539f8-6bbc-4b62-8f7f-b55c7eb242e6""]) { guid }
                }",
        };

        var testSchema = new TestDataContext();
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6") });
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e7") });
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data);
        Assert.NotNull(results.Data!["test"]);
        var testData = (dynamic)results.Data!["test"]!;
        Assert.Single(testData);
        Assert.Equal(Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6"), testData[0].guid);
    }

    [Fact]
    public void TestPropertySetTracking_NotSet_NoVars()
    {
        // don't use variables, use inline values or not
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("test", new TestArgsTracking(), (db, args) => db.People.WhereWhen(p => args.Ids!.Any(a => a == p.Guid), args.IsSet(nameof(TestArgsTracking.Ids))), "test field");
        var gql = new QueryRequest
        {
            Query =
                @"query {
                    test { guid }
                }",
        };

        var testSchema = new TestDataContext();
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6") });
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e7") });
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data);
        Assert.NotNull(results.Data!["test"]);
        var testData = (dynamic)results.Data!["test"]!;
        Assert.Equal(2, testData.Count);
    }

    [Fact]
    public void TestPropertySetTracking_IsSet_Inline()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("test", new TestArgsTracking(), (db, args) => db.People.WhereWhen(p => args.Ids!.Any(a => a == p.Guid), args.IsSet("Ids")), "test field");
        var gql = new QueryRequest
        {
            Query =
                @"query {
                    test(ids: [""03d539f8-6bbc-4b62-8f7f-b55c7eb242e6""]) { guid }
                }",
        };

        var testSchema = new TestDataContext();
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6") });
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e7") });
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data);
        Assert.NotNull(results.Data!["test"]);
        var testData = (dynamic)results.Data!["test"]!;
        Assert.Single(testData);
        Assert.Equal(Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6"), testData[0].guid);
    }

    [Fact]
    public void TestPropertySetTracking_IsSet_Default()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("test", new TestArgsTracking(), (db, args) => db.People.WhereWhen(p => args.Ids!.Any(a => a == p.Guid), args.IsSet("Ids")), "test field");
        var gql = new QueryRequest
        {
            Query =
                @"query ($ids: [ID] = [""03d539f8-6bbc-4b62-8f7f-b55c7eb242e6""]) {
                    test(ids: $ids) { guid }
                }",
        };

        var testSchema = new TestDataContext();
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6") });
        testSchema.People.Add(new Person { Guid = Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e7") });
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data);
        Assert.NotNull(results.Data!["test"]);
        var testData = (dynamic)results.Data!["test"]!;
        Assert.Single(testData);
        Assert.Equal(Guid.Parse("03d539f8-6bbc-4b62-8f7f-b55c7eb242e6"), testData[0].guid);
    }

    [Fact]
    public void TestPropertySetTrackingMutation_IsSet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Mutation()
            .Add(
                "doTest",
                ([GraphQLArguments] TestArgsTracking args) =>
                {
                    return args.IsSet("Ids");
                }
            );
        var gql = new QueryRequest
        {
            Query =
                @"mutation M ($ids: [ID]) {
                    doTest(ids: $ids)
                }",
            Variables = new QueryVariables { { "ids", new[] { "03d539f8-6bbc-4b62-8f7f-b55c7eb242e6" } } },
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
        var testData = (dynamic)results.Data!["doTest"]!;
        Assert.True(testData);
    }

    [Fact]
    public void TestPropertySetTrackingMutation_NotSet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Mutation()
            .Add(
                "doTest",
                ([GraphQLArguments] TestArgsTracking args) =>
                {
                    return args.IsSet("Ids");
                }
            );
        var gql = new QueryRequest
        {
            Query =
                @"mutation M ($ids: [ID]) {
                    doTest(ids: $ids)
                }",
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
        var testData = (dynamic)results.Data!["doTest"]!;
        Assert.False(testData);
    }

    [Fact]
    public void TestPropertySetTrackingMutation_IsSet_NoVars()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Mutation()
            .Add(
                "doTest",
                ([GraphQLArguments] TestArgsTracking args) =>
                {
                    return args.IsSet("Ids");
                }
            );
        var gql = new QueryRequest
        {
            Query =
                @"mutation M {
                    doTest(ids: [""03d539f8-6bbc-4b62-8f7f-b55c7eb242e6""])
                }",
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
        var testData = (dynamic)results.Data!["doTest"]!;
        Assert.True(testData);
    }

    [Fact]
    public void TestPropertySetTrackingMutation_NotSet_NoVars()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Mutation()
            .Add(
                "doTest",
                ([GraphQLArguments] TestArgsTracking args) =>
                {
                    return args.IsSet("Ids");
                }
            );
        var gql = new QueryRequest
        {
            Query =
                @"mutation M {
                    doTest
                }",
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
        var testData = (dynamic)results.Data!["doTest"]!;
        Assert.False(testData);
    }

    [Fact]
    public void TestInputTypePropertySetTrackingMutation_IsSet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputTracking>(nameof(TestInputTracking)).AddAllFields();
        schema.Mutation().Add("doTest", (TestInputTracking input) => input);
        var gql = new QueryRequest
        {
            Query = """
                mutation M ($input: TestInputTracking) {
                    doTest(input : $input)
                }
                """,
            Variables = new QueryVariables()
            {
                {
                    "input",
                    new Dictionary<string, object?>() { { "id", "03d539f8-6bbc-4b62-8f7f-b55c7eb242e6" } }
                },
            },
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
        var testData = (IArgumentsTracker)results.Data!["doTest"]!;
        Assert.True(testData.IsSet(nameof(TestInputTracking.Id)));
    }

    [Fact]
    public void TestInputTypePropertySetTrackingMutation_NotSet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputTracking>(nameof(TestInputTracking)).AddAllFields();
        schema.Mutation().Add("doTest", (TestInputTracking input) => input);
        var gql = new QueryRequest
        {
            Query = """
                mutation M () {
                    doTest(input : {})
                }
                """,
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
        var testData = (IArgumentsTracker)results.Data!["doTest"]!;
        Assert.False(testData.IsSet(nameof(TestInputTracking.Id)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TestNestedInputTypePropertySetTrackingMutation_IsSet(bool setParent, bool setChild)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputTracking>(nameof(TestInputTracking)).AddAllFields();
        schema.AddInputType<NestedTestInputTracking>(nameof(NestedTestInputTracking)).AddAllFields();
        schema.Mutation().Add("doTest", (NestedTestInputTracking input) => input);
        var gql = new QueryRequest
        {
            Query = $$"""
                mutation M () {
                doTest(input : {
                        {{(setParent ? "id: \"03d539f8-6bbc-4b62-8f7f-b55c7eb242e6\"" : "")}}
                        child: {
                            {{(setChild ? "id: \"03d539f8-6bbc-4b62-8f7f-b55c7eb242e7\"" : "")}}
                        } 
                    })
                }
                """,
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
        var testData = (NestedTestInputTracking)results.Data!["doTest"]!;
        Assert.NotNull(testData.Child);
        Assert.Equal(setParent, testData.IsSet(nameof(NestedTestInputTracking.Id)));
        Assert.Equal(setChild, testData.Child.IsSet(nameof(TestInputTracking.Id)));
    }

    [Fact]
    public void TestPersistedInputTypePropertySetTrackingMutation_IsSet()
    {
        var testSchema = new TestDataContext();
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputTracking>(nameof(TestInputTracking)).AddAllFields();
        schema.Mutation().Add("doTest", (TestInputTracking input) => input);

        var query = """
            mutation M ($input: TestInputTracking) {
                doTest(input : $input)
            }
            """;
        var hash = QueryCache.ComputeHash(query);

        var gql = new QueryRequest
        {
            Query = query,
            Variables = new QueryVariables()
            {
                {
                    "input",
                    new Dictionary<string, object?>() { { "id", "03d539f8-6bbc-4b62-8f7f-b55c7eb242e6" } }
                },
            },
            Extensions = new QueryExtensions
            {
                {
                    "persistedQuery",
                    new PersistedQueryExtension { Sha256Hash = hash }
                },
            },
        };

        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        var testData = (IArgumentsTracker)results.Data!["doTest"]!;
        Assert.True(testData.IsSet(nameof(TestInputTracking.Id)));
        Assert.False(testData.IsSet(nameof(TestInputTracking.Name)));

        gql.Query = null;
        gql.Variables = new QueryVariables()
        {
            {
                "input",
                new Dictionary<string, object?>() { { "name", "set this not the id for this run" } }
            },
        };

        results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        testData = (IArgumentsTracker)results.Data!["doTest"]!;
        Assert.True(testData.IsSet(nameof(TestInputTracking.Name)));
        Assert.False(testData.IsSet(nameof(TestInputTracking.Id)));
    }

    [Fact]
    public void TestMutationBasicParams_Vars()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputTracking>(nameof(TestInputTracking)).AddAllFields();
        schema
            .Mutation()
            .Add(
                "doTest",
                (Guid? id, string? name, IArgumentsTracker argsSet) =>
                {
                    Assert.True(argsSet.IsSet(nameof(id)));
                    Assert.False(argsSet.IsSet(nameof(name)));
                    return (id?.ToString() ?? name) ?? "blah";
                }
            );
        var gql = new QueryRequest
        {
            Query = """
                mutation M ($id: ID) {
                    doTest(id : $id)
                }
                """,
            Variables = new QueryVariables() { { "id", "03d539f8-6bbc-4b62-8f7f-b55c7eb242e6" } },
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
    }

    [Fact]
    public void TestMutationBasicParams_Vars_NotSet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputTracking>(nameof(TestInputTracking)).AddAllFields();
        schema
            .Mutation()
            .Add(
                "doTest",
                (Guid? id, string? name, IArgumentsTracker argsSet) =>
                {
                    Assert.False(argsSet.IsSet(nameof(id)));
                    Assert.False(argsSet.IsSet(nameof(name)));
                    return (id?.ToString() ?? name) ?? "blah";
                }
            );
        var gql = new QueryRequest
        {
            Query = """
                mutation M ($id: ID) {
                    doTest(id : $id)
                }
                """,
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
    }

    [Fact]
    public void TestMutationBasicParams_NoVars_Set()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputTracking>(nameof(TestInputTracking)).AddAllFields();
        schema
            .Mutation()
            .Add(
                "doTest",
                (Guid? id, string? name, IArgumentsTracker argsSet) =>
                {
                    Assert.True(argsSet.IsSet(nameof(id)));
                    Assert.False(argsSet.IsSet(nameof(name)));
                    return (id?.ToString() ?? name) ?? "blah";
                }
            );
        var gql = new QueryRequest
        {
            Query = """
                mutation M {
                    doTest(id: "03d539f8-6bbc-4b62-8f7f-b55c7eb242e6")
                }
                """,
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
    }

    [Fact]
    public void TestMutationBasicParams_NoVars_NotSet()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputTracking>(nameof(TestInputTracking)).AddAllFields();
        schema
            .Mutation()
            .Add(
                "doTest",
                (Guid? id, string? name, IArgumentsTracker argsSet) =>
                {
                    Assert.False(argsSet.IsSet(nameof(id)));
                    Assert.False(argsSet.IsSet(nameof(name)));
                    return (id?.ToString() ?? name) ?? "blah";
                }
            );
        var gql = new QueryRequest
        {
            Query = """
                mutation M {
                    doTest
                }
                """,
        };

        var testSchema = new TestDataContext();
        var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.Null(results.Errors);
        Assert.NotNull(results.Data!["doTest"]);
    }

    private class TestArgsTracking : ArgumentsTracker
    {
        public List<Guid>? Ids { get; set; }
    }

    private class TestInputTracking : ArgumentsTracker
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
    }

    private class NestedTestInputTracking : ArgumentsTracker
    {
        public Guid? Id { get; set; }
        public TestInputTracking? Child { get; set; }
    }
}
