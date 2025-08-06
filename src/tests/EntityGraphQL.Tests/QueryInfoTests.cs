using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Subscriptions;
using Xunit;

namespace EntityGraphQL.Tests;

public class QueryInfoTests
{
    [Fact]
    public void TestQueryInfo_IncludeQueryInfoEnabled_Query()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var options = new ExecutionOptions { IncludeQueryInfo = true };

        var gql = new QueryRequest
        {
            Query =
                @"query GetPeople {
                people { 
                    id
                    name
                    projects {
                        id
                        name
                    }
                }
            }",
        };

        var context = new TestDataContext().FillWithTestData();
        var result = schema.ExecuteRequestWithContext(gql, context, null, null, options);

        Assert.Null(result.Errors);
        Assert.NotNull(result.Extensions);
        Assert.True(result.Extensions.ContainsKey("queryInfo"));

        var queryInfo = result.Extensions["queryInfo"];
        Assert.NotNull(queryInfo);

        // Cast to dynamic to access properties
        dynamic info = queryInfo;
        Assert.Equal(GraphQLOperationType.Query, info.OperationType);
        Assert.Equal("GetPeople", info.OperationName);
        Assert.Equal(3, info.TotalTypesQueried);
        Assert.Contains("Person", info.TypesQueried);
        Assert.Contains("Project", info.TypesQueried);
        Assert.Contains("Query", info.TypesQueried);
        Assert.Equal(6, info.TotalFieldsQueried);
        Assert.NotNull(info.TypesQueried);
    }

    [Fact]
    public void TestQueryInfo_IncludeQueryInfoEnabled_Mutation()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddMutationsFrom<TestMutations>();
        var options = new ExecutionOptions { IncludeQueryInfo = true };

        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson {
                addPerson(name: ""John"") {
                    id
                    name
                }
            }",
        };

        var context = new TestDataContext();
        var result = schema.ExecuteRequestWithContext(gql, context, null, null, options);

        Assert.NotNull(result.Extensions);
        Assert.True(result.Extensions.ContainsKey("queryInfo"));

        var queryInfo = result.Extensions["queryInfo"];
        Assert.NotNull(queryInfo);

        // Cast to dynamic to access properties
        dynamic info = queryInfo;
        Assert.Equal(GraphQLOperationType.Mutation, info.OperationType);
        Assert.Equal("AddPerson", info.OperationName);

        // Verify we have some meaningful data collected
        Assert.Equal(2, info.TotalTypesQueried);
        Assert.Equal(3, info.TotalFieldsQueried);
        Assert.Contains("Person", info.TypesQueried);
        Assert.Contains("Mutation", info.TypesQueried);
    }

    [Fact]
    public void TestQueryInfo_IncludeQueryInfoDisabled()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var options = new ExecutionOptions { IncludeQueryInfo = false };

        var gql = new QueryRequest
        {
            Query =
                @"query {
                people { id name }
            }",
        };

        var context = new TestDataContext().FillWithTestData();
        var result = schema.ExecuteRequestWithContext(gql, context, null, null, options);

        Assert.Null(result.Errors);
        // Extensions should either be null or not contain queryInfo
        if (result.Extensions != null)
        {
            Assert.False(result.Extensions.ContainsKey("queryInfo"));
        }
    }

    [Fact]
    public void TestQueryInfo_DefaultOptions()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                people { id name }
            }",
        };

        var context = new TestDataContext().FillWithTestData();
        var result = schema.ExecuteRequestWithContext(gql, context, null, null);

        Assert.Null(result.Errors);
        // Default should not include query info
        if (result.Extensions != null)
        {
            Assert.False(result.Extensions.ContainsKey("queryInfo"));
        }
    }

    [Fact]
    public void TestQueryInfo_WithFragments()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var options = new ExecutionOptions { IncludeQueryInfo = true };

        var gql = new QueryRequest
        {
            Query =
                @"query GetPeopleWithFragments {
                people {
                    ...personDetails
                    projects {
                        ...projectDetails
                    }
                }
            }
            
            fragment personDetails on Person {
                id
                name
            }
            
            fragment projectDetails on Project {
                id
                name
            }",
        };

        var context = new TestDataContext().FillWithTestData();
        var result = schema.ExecuteRequestWithContext(gql, context, null, null, options);

        Assert.Null(result.Errors);
        Assert.NotNull(result.Extensions);
        Assert.True(result.Extensions.ContainsKey("queryInfo"));

        var queryInfo = result.Extensions["queryInfo"];
        Assert.NotNull(queryInfo);

        // Cast to dynamic to access properties
        dynamic info = queryInfo;
        Assert.Equal(GraphQLOperationType.Query, info.OperationType);
        Assert.Equal("GetPeopleWithFragments", info.OperationName);
        Assert.True(info.TotalFieldsQueried > 0);

        // Verify specific types and fields were queried (same as first test since fragments expand to same fields)
        var typesQueried = (Dictionary<string, HashSet<string>>)info.TypesQueried;
        Assert.True(typesQueried.ContainsKey("Person"));
        Assert.True(typesQueried.ContainsKey("Project"));
        Assert.True(typesQueried.ContainsKey("Query"));

        // Verify Person fields (from personDetails fragment)
        Assert.Contains("id", typesQueried["Person"]);
        Assert.Contains("name", typesQueried["Person"]);
        Assert.Contains("projects", typesQueried["Person"]);

        // Verify Project fields (from projectDetails fragment)
        Assert.Contains("id", typesQueried["Project"]);
        Assert.Contains("name", typesQueried["Project"]);

        // Verify we have reasonable counts
        Assert.Equal(3, info.TotalTypesQueried);
        Assert.Equal(6, info.TotalFieldsQueried);
    }

    [Fact]
    public void TestQueryInfo_TypesQueried_HasData()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var options = new ExecutionOptions { IncludeQueryInfo = true };

        var gql = new QueryRequest
        {
            Query =
                @"query SimpleTest {
                people { 
                    id 
                    name 
                }
            }",
        };

        var context = new TestDataContext().FillWithTestData();
        var result = schema.ExecuteRequestWithContext(gql, context, null, null, options);

        Assert.Null(result.Errors);
        var queryInfo = result.Extensions!["queryInfo"];

        // Cast to QueryInfo to verify structure
        var actualQueryInfo = (QueryInfo)queryInfo!;

        // Verify we have collected type and field information
        Assert.Equal(3, actualQueryInfo.TotalFieldsQueried);
        Assert.Equal(2, actualQueryInfo.TotalTypesQueried);
        Assert.True(actualQueryInfo.TypesQueried.ContainsKey("Person"));
        Assert.True(actualQueryInfo.TypesQueried.ContainsKey("Query"));
        Assert.Equal(actualQueryInfo.TypesQueried.Count, actualQueryInfo.TotalTypesQueried);

        // Verify field counts are consistent
        var totalFields = actualQueryInfo.TypesQueried.Values.Sum(fields => fields.Count);
        Assert.Equal(totalFields, actualQueryInfo.TotalFieldsQueried);
    }

    [Fact]
    public void TestQueryInfo_IncludeQueryInfoEnabled_Subscription()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Subscription().AddFrom<TestSubscriptions>();
        var options = new ExecutionOptions { IncludeQueryInfo = true };

        var gql = new QueryRequest
        {
            Query =
                @"subscription PersonAdded {
                personAdded {
                    id
                    name
                }
            }",
        };

        var context = new TestDataContext();
        var result = schema.ExecuteRequestWithContext(gql, context, null, null, options);

        Assert.NotNull(result.Extensions);
        Assert.True(result.Extensions.ContainsKey("queryInfo"));

        var queryInfo = result.Extensions["queryInfo"];
        Assert.NotNull(queryInfo);

        // Cast to dynamic to access properties
        dynamic info = queryInfo;
        Assert.Equal(GraphQLOperationType.Subscription, info.OperationType);
        Assert.Equal("PersonAdded", info.OperationName);

        // Verify we have some meaningful data collected
        Assert.Equal(2, info.TotalTypesQueried);
        Assert.Equal(3, info.TotalFieldsQueried);
        Assert.Contains("Subscription", info.TypesQueried);
    }

    private class TestSubscriptions
    {
        [GraphQLSubscription("Person added")]
        public static IObservable<Person> PersonAdded() => new Broadcaster<Person>();
    }
}

public class TestMutations
{
    [GraphQLMutation("Add a new person")]
    public static Person AddPerson(TestDataContext context, string name)
    {
        var person = new Person { Name = name, Id = context.People.Count + 1 };
        context.People.Add(person);
        return person;
    }
}
