using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class PersistedQueriesTests
{
    [Fact]
    public void TestPersistedQueryLookup()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var query = @"{
                    project(id: 99) {
                        name
                        tasks {
                            name id
                        }
                    }
                }";
        var hash = QueryCache.ComputeHash(query);

        var gql = new QueryRequest
        {
            Query = query,
            Extensions = new Dictionary<string, Dictionary<string, object>>
            {
                { "persistedQuery", new PersistedQueryExtension { Sha256Hash = hash } }
            }
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnablePersistedQueries = true });
        CheckResult(result);

        // look no query!
        gql.Query = null;
        result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnablePersistedQueries = true });
        CheckResult(result);

        static void CheckResult(QueryResult result)
        {
            Assert.Null(result.Errors);

            dynamic project = result.Data["project"];
            Assert.Equal(5, Enumerable.Count(project.tasks));
        }
    }

    [Fact]
    public void TestPersistedQueryLookupNotFound()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var query = @"{
                    project(id: 99) {
                        name
                        tasks {
                            name id
                        }
                    }
                }";
        var hash = QueryCache.ComputeHash(query);

        var gql = new QueryRequest
        {
            Query = null, // assume it is cached
            Extensions = new Dictionary<string, Dictionary<string, object>>
            {
                { "persistedQuery", new PersistedQueryExtension { Sha256Hash = hash } }
            }
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnablePersistedQueries = true });
        Assert.Single(result.Errors);

        Assert.Equal("PersistedQueryNotFound", result.Errors.First().Message);
    }

    [Fact]
    public void TestPersistedQueryNotSupported()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var query = @"{
                    project(id: 99) {
                        name
                        tasks {
                            name id
                        }
                    }
                }";
        var hash = QueryCache.ComputeHash(query);

        var gql = new QueryRequest
        {
            Query = null, // assume it is cached
            Extensions = new Dictionary<string, Dictionary<string, object>>
            {
                { "persistedQuery", new PersistedQueryExtension { Sha256Hash = hash } }
            }
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnablePersistedQueries = false });
        Assert.Single(result.Errors);

        Assert.Equal("PersistedQueryNotSupported", result.Errors.First().Message);
    }
    [Fact]
    public void TestPersistedQueryNotSupportedWrongVersion()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var query = @"{
                    project(id: 99) {
                        name
                        tasks {
                            name id
                        }
                    }
                }";
        var hash = QueryCache.ComputeHash(query);

        var gql = new QueryRequest
        {
            Query = query,
            Extensions = new Dictionary<string, Dictionary<string, object>>
            {
                { "persistedQuery", new PersistedQueryExtension { Sha256Hash = hash, Version = 2 } }
            }
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnablePersistedQueries = true });
        Assert.Single(result.Errors);

        Assert.Equal("PersistedQueryNotSupported", result.Errors.First().Message);
    }

    private static void FillProjectData(TestDataContext data)
    {
        data.Projects = new List<Project>
            {
                new Project
                {
                    Id = 99,
                    Name ="Project 1",
                    Tasks = new List<Task>
                    {
                        new Task
                        {
                            Id = 0,
                            Name = "Task 1"
                        },
                        new Task
                        {
                            Id = 1,
                            Name = "Task 2"
                        },
                        new Task
                        {
                            Id = 2,
                            Name = "Task 3"
                        },
                        new Task
                        {
                            Id = 3,
                            Name = "Task 4"
                        },
                        new Task
                        {
                            Id = 4,
                            Name = "Task 5"
                        },

                    }
                }
            };
    }

}