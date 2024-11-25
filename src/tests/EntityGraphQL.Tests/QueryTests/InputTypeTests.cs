using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class InputTypeTests
{
    [Fact]
    public void SupportsEnumInInputType_Introspection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<PeopleArgs>("PeopleArgs", "people filter args").AddAllFields();
        schema.Query().ReplaceField("people", new { args = (PeopleArgs?)null }, (p, param) => p.People, "Return people");
        var gql = new QueryRequest
        {
            Query =
                @"query {
                        __type(name: ""PeopleArgs"") {
                            name
                            fields {
                                name
                            }
                        }
                    }",
        };
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "unit");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "dayOfWeek");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "name");
    }

    [Fact]
    public void SupportsEnumInInputTypeAsList_Introspection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<PeopleArgs>("PeopleArgs", "people filter args").AddAllFields();
        schema.Query().ReplaceField("people", new { args = (List<PeopleArgs>?)null }, (p, param) => p.People, "Return people");
        var gql = new QueryRequest
        {
            Query =
                @"query {
                        __type(name: ""PeopleArgs"") {
                            name
                            fields {
                                name
                            }
                        }
                    }",
        };
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "unit");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "dayOfWeek");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "name");
    }

    [Fact]
    public void SupportsEnumInInputTypeAsListInMutation_Introspection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // testing that auto creation of mutation args does correctly add the type
        schema
            .Mutation()
            .Add(
                "AddPerson",
                "Description",
                ([GraphQLInputType] List<PeopleArgs> args) =>
                {
                    return true;
                }
            );
        var gql = new QueryRequest
        {
            Query =
                @"query {
                        __type(name: ""PeopleArgs"") {
                            name
                            fields {
                                name
                            }
                        }
                    }",
        };
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "unit");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "dayOfWeek");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "name");
    }

    [Fact]
    public void SupportsEnumInInputTypeAsListInMutationArgs_Introspection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // testing that auto creation of mutation args does correctly add the type
        schema
            .Mutation()
            .Add(
                "AddPerson",
                "Description",
                ([GraphQLArguments] TestMutationArgs args) =>
                {
                    return true;
                }
            );
        var gql = new QueryRequest
        {
            Query =
                @"query {
                        __type(name: ""PeopleArgs"") {
                            name
                            fields {
                                name
                            }
                        }
                    }",
        };
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "unit");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "dayOfWeek");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "name");
    }

    class TaskInput : Task { }

    class UserInput : User { }

    [Fact]
    public void SupportsQueryTypeReturnCorrectlyFindsTheInputTypeReturn()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();

        schema.AddType<User>("User");
        schema.AddType<Task>("Task");

        schema.UpdateType<User>(type =>
        {
            type.AddField("id", x => x.Id, null);
            type.AddField("tasks", x => x.Tasks, null);
        });

        schema.UpdateType<Task>(type =>
        {
            type.AddField("id", x => x.Id, null);
            type.AddField("user", null).Resolve<TestDataContext>((p, ctx) => ctx.Users.FirstOrDefault(u => u.Id == p.Project!.CreatedBy));
        });

        var taskInput = schema.AddInputType<TaskInput>("TaskInput");
        taskInput.AddField("id", x => x.Id, null);

        var userInput = schema.AddInputType<UserInput>("UserInput");
        userInput.AddField("id", x => x.Id, null);
        // This returns Task but we should figure out we need TaskInput
        userInput.AddField("tasks", x => x.Tasks, null);

        schema.Query().AddField("users", (ctx) => ctx.Users, "Get a users");
        schema.Query().AddField("tasks", (ctx) => ctx.Tasks, "Get a tasks");

        var result = schema.ToGraphQLSchemaString();
        Assert.Contains("input UserInput {\n\tid: Int!\n\ttasks: [TaskInput!]\n}", result);
    }

    private static SchemaProvider<TestDataContext> SetUpSchemaForQueryAsInputTypeTests()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();

        schema.AddType<User>("User");
        schema.AddType<Task>("Task");

        schema.UpdateType<User>(type =>
        {
            type.AddField("id", x => x.Id, null);
            type.AddField("tasks", x => x.Tasks, null);
        });

        schema.UpdateType<Task>(type =>
        {
            type.AddField("id", x => x.Id, null);
            type.AddField("user", null).Resolve<TestDataContext>((p, ctx) => ctx.Users.FirstOrDefault(u => u.Id == p.Project!.CreatedBy));
        });

        var taskInput = schema.AddInputType<Task>("TaskInput");
        taskInput.AddField("id", x => x.Id, null);

        var userInput = schema.AddInputType<User>("UserInput");
        userInput.AddField("id", x => x.Id, null);
        userInput.AddField("tasks", x => x.Tasks, null);

        schema.Query().AddField("users", (ctx) => ctx.Users, "Get a users");
        schema.Query().AddField("tasks", (ctx) => ctx.Tasks, "Get a tasks");
        return schema;
    }

    [Fact]
    public void SupportsQueryTypeAsInputType()
    {
        var schema = SetUpSchemaForQueryAsInputTypeTests();

        var result = schema.ToGraphQLSchemaString();
        Assert.Contains("input UserInput {\n\tid: Int!\n\ttasks: [TaskInput!]\n}", result);
    }

    [Fact]
    public void SupportsQueryTypeAsInputTypeIntrospection()
    {
        var schema = SetUpSchemaForQueryAsInputTypeTests();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                        __type(name: ""UserInput"") {
                            name
                            fields {
                                name
                                type { name ofType { kind name } }
                            }
                        }
                    }",
        };
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "id");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields, f => f.name == "tasks");
        Assert.Equal("TaskInput", ((dynamic)result.Data!["__type"]!).fields[1].type.ofType.name);
    }

    [Fact]
    public void SupportsQueryTypeAsInputTypeUsingReturns()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();

        schema.AddType<User>("User");
        schema.AddType<Task>("Task");

        schema.UpdateType<User>(type =>
        {
            type.AddField("id", x => x.Id, null);
            type.AddField("tasks", x => x.Tasks, null);
        });

        schema.UpdateType<Task>(type =>
        {
            type.AddField("id", x => x.Id, null);
            type.AddField("user", null).Resolve<TestDataContext>((p, ctx) => ctx.Users.FirstOrDefault(u => u.Id == p.Project!.CreatedBy));
        });

        var taskInput = schema.AddInputType<Task>("TaskInput");
        taskInput.AddField("id", x => x.Id, null);

        var userInput = schema.AddInputType<User>("UserInput");
        userInput.AddField("id", x => x.Id, null);
        userInput
            .AddField("tasks", x => x.Tasks, null)
            // Here we tell it what it returns
            .Returns("TaskInput");

        schema.Query().AddField("users", (ctx) => ctx.Users, "Get a users");
        schema.Query().AddField("tasks", (ctx) => ctx.Tasks, "Get a tasks");

        var result = schema.ToGraphQLSchemaString();
        Assert.Contains("input UserInput {\n\tid: Int!\n\ttasks: [TaskInput!]\n}", result);
    }

    [GraphQLArguments]
    internal class TestMutationArgs
    {
        public List<PeopleArgs> People { get; set; } = [];
    }

    internal class PeopleArgs
    {
        // System enum
        public DayOfWeek DayOfWeek { get; set; }
        public HeightUnit Unit { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
