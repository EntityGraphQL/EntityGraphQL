using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
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

        schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata").UseConnectionPaging(defaultPageSize: 2);

        var query =
            @"query This($project: Int!){
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
            Variables = new QueryVariables { { "project", 99 } },
        };

        // cache the query
        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnableQueryCache = true });
        CheckResults(result, 99);

        // will be from cache
        gql.Variables = new QueryVariables { { "project", 1 } };
        result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { EnableQueryCache = true });
        CheckResults(result, 1);

        static void CheckResults(QueryResult result, int projectId)
        {
            Assert.Null(result.Errors);

            dynamic project = result.Data!["project"]!;
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

        schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Return list of task with paging metadata").UseConnectionPaging(defaultPageSize: 2);

        var query =
            @"query This($project: Int!){
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
            Variables = new QueryVariables { { "project", 99 } },
        };

        var total = 1000;
        var failed = new List<string>();
        var writeLock = new object();

        Parallel.For(
            0,
            total,
            _ =>
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
            }
        );

        Assert.Empty(failed);

        static void CheckResults(QueryResult result, int projectId)
        {
            Assert.Null(result.Errors);

            dynamic project = result.Data!["project"]!;
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

    // ---- delegate cache tests -------------------------------------------------

    [Fact]
    public void DelegateCache_SameVariables_ReturnsSameResult()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var gql = new QueryRequest
        {
            Query = "query P($project: Int!){ project(id: $project) { name id } }",
            Variables = new QueryVariables { { "project", 99 } },
        };
        var options = new ExecutionOptions { EnableQueryCache = true, CacheCompiledDelegates = true };

        var r1 = schema.ExecuteRequestWithContext(gql, data, null, null, options);
        var r2 = schema.ExecuteRequestWithContext(gql, data, null, null, options);

        Assert.Null(r1.Errors);
        Assert.Null(r2.Errors);
        Assert.Equal(99, ((dynamic)r1.Data!["project"]!).id);
        Assert.Equal(99, ((dynamic)r2.Data!["project"]!).id);
    }

    [Fact]
    public void DelegateCache_DifferentVariables_ReturnsDifferentResults()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var options = new ExecutionOptions { EnableQueryCache = true, CacheCompiledDelegates = true };
        var q = "query P($project: Int!){ project(id: $project) { name id } }";

        var r1 = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = q,
                Variables = new QueryVariables { { "project", 99 } },
            },
            data,
            null,
            null,
            options
        );
        var r2 = schema.ExecuteRequestWithContext(
            new QueryRequest
            {
                Query = q,
                Variables = new QueryVariables { { "project", 1 } },
            },
            data,
            null,
            null,
            options
        );

        Assert.Null(r1.Errors);
        Assert.Null(r2.Errors);
        Assert.Equal(99, ((dynamic)r1.Data!["project"]!).id);
        Assert.Equal(1, ((dynamic)r2.Data!["project"]!).id);
    }

    [Fact]
    public void DelegateCache_BeforeExecuting_BypassesCacheButStillWorks()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var callCount = 0;
        var options = new ExecutionOptions
        {
            EnableQueryCache = true,
            CacheCompiledDelegates = true,
            BeforeExecuting = (expr, _) =>
            {
                callCount++;
                return expr;
            },
        };
        var gql = new QueryRequest
        {
            Query = "query P($project: Int!){ project(id: $project) { name id } }",
            Variables = new QueryVariables { { "project", 99 } },
        };

        var r1 = schema.ExecuteRequestWithContext(gql, data, null, null, options);
        var r2 = schema.ExecuteRequestWithContext(gql, data, null, null, options);

        // hook must be called on every request (cache bypassed), result must still be correct
        Assert.Equal(2, callCount);
        Assert.Null(r2.Errors);
        Assert.Equal(99, ((dynamic)r2.Data!["project"]!).id);
    }

    [Fact]
    public void DelegateCache_WithPaging_CorrectResultsOnCacheHit()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        schema.Type<Project>().ReplaceField("tasks", ctx => ctx.Tasks.OrderBy(p => p.Id), "Tasks").UseConnectionPaging(defaultPageSize: 2);

        var gql = new QueryRequest
        {
            Query =
                @"query P($project: Int!){
                project(id: $project) {
                    tasks { edges { node { id name } } totalCount }
                }
            }",
            Variables = new QueryVariables { { "project", 99 } },
        };
        var options = new ExecutionOptions { EnableQueryCache = true, CacheCompiledDelegates = true };

        var r1 = schema.ExecuteRequestWithContext(gql, data, null, null, options);
        // second call hits delegate cache
        var r2 = schema.ExecuteRequestWithContext(gql, data, null, null, options);

        Assert.Null(r1.Errors);
        Assert.Null(r2.Errors);
        dynamic project2 = r2.Data!["project"]!;
        Assert.Equal(5, project2.tasks.totalCount);
        Assert.Equal(2, Enumerable.Count(project2.tasks.edges));
    }

    [Fact]
    public void DelegateCache_Concurrent_NoCorruption()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var options = new ExecutionOptions { EnableQueryCache = true, CacheCompiledDelegates = true };
        var gql = new QueryRequest
        {
            Query = "query P($project: Int!){ project(id: $project) { name id } }",
            Variables = new QueryVariables { { "project", 99 } },
        };

        var failed = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(
            0,
            200,
            _ =>
            {
                try
                {
                    var result = schema.ExecuteRequestWithContext(gql, data, null, null, options);
                    if (result.Errors != null)
                        failed.Add(string.Join(", ", result.Errors.Select(e => e.Message)));
                    else if ((int)((dynamic)result.Data!["project"]!).id != 99)
                        failed.Add("wrong project id");
                }
                catch (Exception ex)
                {
                    failed.Add(ex.Message);
                }
            }
        );

        Assert.Empty(failed);
    }

    [Fact]
    public void DelegateCache_TwoPassServiceField_CorrectResultsOnCacheHit()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
            type.ReplaceField("createdBy", "User who created this project")
                .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedBy))
        );

        var gql = new QueryRequest
        {
            Query = "{ projects { name createdBy { id field2 } } }",
        };
        var options = new ExecutionOptions { EnableQueryCache = true, CacheCompiledDelegates = true };

        var ctx = new TestDataContext
        {
            Projects = [new Project { Id = 1, CreatedBy = 1, Name = "Proj1" }],
        };
        var sp = new ServiceCollection()
            .AddSingleton(ctx)
            .AddSingleton(new UserService())
            .BuildServiceProvider();

        // first call: both pass-1 and pass-2 delegates compiled and cached
        var r1 = schema.ExecuteRequest(gql, sp, null, options);
        // second call: both delegates come from cache
        var r2 = schema.ExecuteRequest(gql, sp, null, options);

        Assert.Null(r1.Errors);
        Assert.Null(r2.Errors);
        dynamic proj2 = ((dynamic)r2.Data!["projects"]!)[0];
        Assert.Equal("Proj1", proj2.name);
        Assert.Equal(1, proj2.createdBy.id);
        Assert.Equal("SingleCall", proj2.createdBy.field2);
    }

    [Fact]
    public void DelegateCache_BulkResolver_CorrectResultsOnCacheHit()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
            type.ReplaceField("createdBy", "User who created this project")
                .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedBy))
                .ResolveBulk<UserService, int, User>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsers(ids))
        );

        var gql = new QueryRequest
        {
            Query = "{ projects { name createdBy { id name } } }",
        };
        var options = new ExecutionOptions { EnableQueryCache = true, CacheCompiledDelegates = true };

        var ctx = new TestDataContext
        {
            Projects =
            [
                new Project { Id = 1, CreatedBy = 1, Name = "Proj1" },
                new Project { Id = 2, CreatedBy = 2, Name = "Proj2" },
            ],
        };
        var userService = new UserService();
        var sp = new ServiceCollection()
            .AddSingleton(ctx)
            .AddSingleton(userService)
            .BuildServiceProvider();

        var r1 = schema.ExecuteRequest(gql, sp, null, options);
        var r2 = schema.ExecuteRequest(gql, sp, null, options);

        Assert.Null(r1.Errors);
        Assert.Null(r2.Errors);
        // bulk resolver called once per execution (not once total)
        Assert.Equal(2, userService.CallCount);
        dynamic projs = r2.Data!["projects"]!;
        Assert.Equal(2, projs.Count);
        Assert.Equal(1, projs[0].createdBy.id);
        Assert.Equal("Name_1", projs[0].createdBy.name);
        Assert.Equal(2, projs[1].createdBy.id);
        Assert.Equal("Name_2", projs[1].createdBy.name);
    }

    private static void FillProjectData(TestDataContext data)
    {
        var tasks = new List<Task>
        {
            new Task { Id = 0, Name = "Task 1" },
            new Task { Id = 1, Name = "Task 2" },
            new Task { Id = 2, Name = "Task 3" },
            new Task { Id = 3, Name = "Task 4" },
            new Task { Id = 4, Name = "Task 5" },
        };
        data.Projects =
        [
            new Project
            {
                Id = 99,
                Name = "Project 1",
                Tasks = tasks,
            },
            new Project
            {
                Id = 1,
                Name = "Project 1",
                Tasks = tasks,
            },
        ];
    }
}
