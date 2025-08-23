using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class BulkAsyncTests
{
    [Fact]
    public void TestAsyncBulkResolverFullObject()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .ResolveAsync<AsyncUserService>((proj, users) => users.GetUserByIdAsync(proj.CreatedBy))
                .ResolveBulkAsync<AsyncUserService, int, User>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsersAsync(ids));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name createdBy { id field2 } 
                } 
            }",
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1",
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2",
                },
            ],
        };
        var serviceCollection = new ServiceCollection();
        AsyncUserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project due to bulk resolving
        Assert.Equal(1, userService.CallCount);
        // verify we have correct data for all projects
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects.Count);
        for (int i = 0; i < 2; i++)
        {
            var project = projects[i];
            Assert.Equal(i + 1, project.createdBy.id);
            Assert.Equal("Hello", project.createdBy.field2);
        }
    }

    [Fact]
    public void TestAsyncBulkResolverWithConcurrencyLimit()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        // Add multiple fields that each use bulk resolvers to force concurrent execution
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .ResolveAsync<ConcurrencyTrackingAsyncUserService>((proj, users) => users.GetUserByIdAsync(proj.CreatedBy))
                .ResolveBulkAsync<ConcurrencyTrackingAsyncUserService, int, User>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsersAsync(ids), maxConcurrency: 2);

            type.AddField("assignedUser", "Get assigned user")
                .ResolveAsync<ConcurrencyTrackingAsyncUserService>((proj, users) => users.GetUserByIdAsync(proj.CreatedBy + 10))
                .ResolveBulkAsync<ConcurrencyTrackingAsyncUserService, int, User>(proj => proj.CreatedBy + 10, (ids, srv) => srv.GetAllUsersAsync(ids), maxConcurrency: 2);
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name 
                    createdBy { id field2 } 
                    assignedUser { id field2 }
                } 
            }",
        };

        var context = new TestDataContext
        {
            Projects = Enumerable
                .Range(1, 4)
                .Select(i => new Project
                {
                    Id = i,
                    CreatedBy = i,
                    Name = $"Project {i}",
                })
                .ToList(),
        };

        var serviceCollection = new ServiceCollection();
        ConcurrencyTrackingAsyncUserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);

        // We should have made 2 bulk calls (one for createdBy, one for assignedUser)
        Assert.True(userService.CallCount >= 2, $"Expected at least 2 calls, but only had {userService.CallCount}");

        // Verify concurrency was limited to the specified max
        Assert.True(userService.MaxConcurrentOperations == 2, $"Expected max concurrency of 2, but actual max was {userService.MaxConcurrentOperations}");

        // verify we have correct data for all projects
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(4, projects.Count);
        for (int i = 0; i < 4; i++)
        {
            var project = projects[i];
            Assert.Equal(i + 1, project.createdBy.id);
            Assert.Equal("Hello", project.createdBy.field2);
            Assert.Equal(i + 11, project.assignedUser.id);
            Assert.Equal("Hello", project.assignedUser.field2);
        }
    }

    [Fact]
    public void TestAsyncBulkResolverScalarResult()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
        {
            type.AddField("createdByName", "Get user name that created it")
                .ResolveAsync<AsyncUserService>((proj, users) => users.GetAllUserNamesAsync(new[] { proj.CreatedBy }).ContinueWith(task => task.Result[proj.CreatedBy]))
                .ResolveBulkAsync<AsyncUserService, int, string>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUserNamesAsync(ids));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name createdByName
                } 
            }",
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1",
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2",
                },
            ],
        };
        var serviceCollection = new ServiceCollection();
        AsyncUserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects.Count);
        Assert.Equal("Name_1", projects[0].createdByName);
        Assert.Equal("Name_2", projects[1].createdByName);
    }

    [Fact]
    public void TestAsyncBulkResolverWithArguments()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
        {
            type.AddField("user", new { id = ArgumentHelper.Required<int>() }, "Get user by id")
                .Resolve<AsyncUserService>((proj, args, users) => users.GetUserByIdForProjectIdAsync(proj.Id, args.id).GetAwaiter().GetResult()!)
                .ResolveBulkAsync<AsyncUserService, int, User?>(proj => proj.Id, (ids, args, srv) => srv.GetUserByIdForProjectIdAsync(ids, args.id));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name 
                    user(id: 1) { id field2 } 
                } 
            }",
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1",
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2",
                },
            ],
        };
        var serviceCollection = new ServiceCollection();
        AsyncUserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects.Count);
        Assert.Equal(1, projects[0].user.id);
        Assert.Equal("Hello", projects[0].user.field2);
        Assert.Null(projects[1].user); // id 2 != 1, so should be null
    }
}

public class AsyncUserService
{
    public int CallCount { get; private set; }
    public List<string> Calls { get; private set; } = [];

    public async Task<User> GetUserByIdAsync(int id)
    {
        CallCount += 1;
        Calls.Add(nameof(GetUserByIdAsync));
        await System.Threading.Tasks.Task.Yield(); // Simulate async operation
        return new User { Id = id, Field2 = "SingleCall" };
    }

    public async Task<IDictionary<int, User>> GetAllUsersAsync(IEnumerable<int> data)
    {
        CallCount += 1;
        Calls.Add(nameof(GetAllUsersAsync));
        await System.Threading.Tasks.Task.Delay(50); // Simulate async operation
        return data.Distinct()
            .Select(id => new User
            {
                Id = id,
                Field2 = "Hello",
                Name = $"Name_{id}",
            })
            .ToDictionary(u => u.Id, u => u);
    }

    public async Task<IDictionary<int, string>> GetAllUserNamesAsync(IEnumerable<int> data)
    {
        CallCount += 1;
        Calls.Add(nameof(GetAllUserNamesAsync));
        await System.Threading.Tasks.Task.Delay(50); // Simulate async operation
        return data.Distinct().ToDictionary(id => id, id => $"Name_{id}");
    }

    public async Task<User?> GetUserByIdForProjectIdAsync(int projectId, int userId)
    {
        CallCount += 1;
        Calls.Add(nameof(GetUserByIdForProjectIdAsync));
        await System.Threading.Tasks.Task.Yield(); // Simulate async operation
        return projectId != userId ? null : new User { Id = userId, Field2 = "Hello" };
    }

    public async Task<IDictionary<int, User?>> GetUserByIdForProjectIdAsync(IEnumerable<int> projectIds, int userId)
    {
        CallCount += 1;
        Calls.Add(nameof(GetUserByIdForProjectIdAsync));
        await System.Threading.Tasks.Task.Delay(50); // Simulate async operation
        return projectIds.ToDictionary(projectId => projectId, projectId => projectId != userId ? null : new User { Id = userId, Field2 = "Hello" });
    }
}

public class ConcurrencyTrackingAsyncUserService
{
    private int currentConcurrency = 0;
    private int maxConcurrency = 0;
    private int callCount = 0;

    public int MaxConcurrentOperations => maxConcurrency;
    public int CallCount => callCount;

    public async Task<IDictionary<int, User>> GetAllUsersAsync(IEnumerable<int> ids)
    {
        Interlocked.Increment(ref callCount);
        var current = Interlocked.Increment(ref currentConcurrency);
        var max = Math.Max(maxConcurrency, current);
        Interlocked.Exchange(ref maxConcurrency, max);

        try
        {
            await System.Threading.Tasks.Task.Delay(100); // Simulate async work

            return ids.ToDictionary(id => id, id => new User { Id = id, Field2 = "Hello" });
        }
        finally
        {
            Interlocked.Decrement(ref currentConcurrency);
        }
    }

    public async Task<User> GetUserByIdAsync(int id)
    {
        await System.Threading.Tasks.Task.Delay(50);
        return new User { Id = id, Field2 = "Hello" };
    }
}
