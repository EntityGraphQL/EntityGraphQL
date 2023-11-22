using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests;

public class QueryCacheTests
{
    [Fact]
    public void TestCachedQueryDoesNotCacheVariables()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata")
            .UseConnectionPaging(defaultPageSize: 2);

        var query = @"query This($project: Int!){
                    project(id: $project) {
                        name id
                        tasks {
                            edges {
                                node {
                                    name id
                                }
                                cursor
                            }
                            pageInfo {
                                startCursor
                                endCursor
                                hasNextPage
                                hasPreviousPage
                            }
                            totalCount
                        }
                    }
                }";
        var hash = QueryCache.ComputeHash(query);
        var gql = new QueryRequest
        {
            Query = query,
            Variables = new QueryVariables
                {
                    { "project", 99 }
                }
        };

        // cache the query
        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnableQueryCache = true });
        CheckResults(result, 99);

        // will be from cache
        gql.Variables = new QueryVariables
        {
            { "project", 1 }
        };
        result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnableQueryCache = true });
        CheckResults(result, 1);

        static void CheckResults(QueryResult result, int projectId)
        {
            Assert.Null(result.Errors);

            dynamic project = result.Data["project"];
            Assert.Equal(projectId, project.id);
            Assert.Equal(2, Enumerable.Count(project.tasks.edges));
            Assert.Equal(5, project.tasks.totalCount);
            Assert.True(project.tasks.pageInfo.hasNextPage);
            Assert.False(project.tasks.pageInfo.hasPreviousPage);

            // cursors MQ, Mg, Mw, NA, NQ
            // we have tests for (de)serialization of cursor we're checking the correct ones are used
            var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
            var expectedLastCursor = ConnectionHelper.SerializeCursor(2);
            Assert.Equal(expectedFirstCursor, project.tasks.pageInfo.startCursor);
            Assert.Equal(expectedLastCursor, project.tasks.pageInfo.endCursor);
            Assert.Equal(expectedFirstCursor, Enumerable.First(project.tasks.edges).cursor);
            Assert.Equal(expectedLastCursor, Enumerable.Last(project.tasks.edges).cursor);
        }
    }

    [Fact]
    public void TestCachedQueryInParallel()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata")
            .UseConnectionPaging(defaultPageSize: 2);

        var query = @"query This($project: Int!){
                    project(id: $project) {
                        name id
                        tasks {
                            edges {
                                node {
                                    name id
                                }
                                cursor
                            }
                            pageInfo {
                                startCursor
                                endCursor
                                hasNextPage
                                hasPreviousPage
                            }
                            totalCount
                        }
                    }
                }";
        var gql = new QueryRequest
        {
            Query = query,
            Variables = new QueryVariables
                {
                    { "project", 99 }
                }
        };

        var total = 1000;
        var failed = new List<string>();
        var writeLock = new object();

        Parallel.For(0, total, _ =>
        {
            try
            {
                // cache the query
                var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnableQueryCache = true });
                CheckResults(result, 99);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                lock (writeLock)
                {
                    failed.Add(e.Message);
                }
            }
        });

        Assert.Empty(failed);

        static void CheckResults(QueryResult result, int projectId)
        {
            Assert.Null(result.Errors);

            dynamic project = result.Data["project"];
            Assert.Equal(projectId, project.id);
            Assert.Equal(2, Enumerable.Count(project.tasks.edges));
            Assert.Equal(5, project.tasks.totalCount);
            Assert.True(project.tasks.pageInfo.hasNextPage);
            Assert.False(project.tasks.pageInfo.hasPreviousPage);

            // cursors MQ, Mg, Mw, NA, NQ
            // we have tests for (de)serialization of cursor we're checking the correct ones are used
            var expectedFirstCursor = ConnectionHelper.SerializeCursor(1);
            var expectedLastCursor = ConnectionHelper.SerializeCursor(2);
            Assert.Equal(expectedFirstCursor, project.tasks.pageInfo.startCursor);
            Assert.Equal(expectedLastCursor, project.tasks.pageInfo.endCursor);
            Assert.Equal(expectedFirstCursor, Enumerable.First(project.tasks.edges).cursor);
            Assert.Equal(expectedLastCursor, Enumerable.Last(project.tasks.edges).cursor);
        }
    }

    private static void FillProjectData(TestDataContext data)
    {
        var tasks = new List<Task>
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

        };
        data.Projects = new List<Project>
        {
            new Project
            {
                Id = 99,
                Name ="Project 1",
                Tasks = tasks
            },
            new Project
            {
                Id = 1,
                Name ="Project 1",
                Tasks = tasks
            }
        };
    }

}