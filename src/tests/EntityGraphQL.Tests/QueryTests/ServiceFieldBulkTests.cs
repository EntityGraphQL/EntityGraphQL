using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class ServiceFieldBulkTests
{
    [Fact]
    public void TestServicesBulkResolverFullObject()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .ResolveWithService<UserService>((proj, users) => users.GetUserById(proj.CreatedBy))
                .ResolveBulk<UserService, int, User>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsers(ids));
        });

        var gql = new QueryRequest
        {
            Query = @"{ 
                projects { 
                    name createdBy { id field2 } 
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects = new List<Project> {
                new Project { Id = 1, CreatedBy = 1 , Name = "Project 1"},
                new Project { Id = 2, CreatedBy = 2, Name = "Project 2"},
            },
        };
        var serviceCollection = new ServiceCollection();
        UserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic projects = res.Data["projects"];
        Assert.Equal(2, projects.Count);
        var project = projects[0];
        Assert.Equal(2, project.createdBy.GetType().GetFields().Length);
        Assert.Equal(1, project.createdBy.id);
        Assert.Equal("Hello", project.createdBy.field2);
    }

    [Fact]
    public void TestServicesBulkResolverScalar()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema.UpdateType<Project>(type =>
        {
            type.AddField("createdByName", "Get user name that created it")
                .ResolveWithService<UserService>((proj, users) => users.GetUserById(proj.CreatedBy).Name)
                .ResolveBulk<UserService, int, string>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsers(ids).ToDictionary(u => u.Key, u => u.Value.Name));
        });

        var gql = new QueryRequest
        {
            Query = @"{ 
                projects { 
                    name createdByName
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects = new List<Project> {
                new Project { Id = 1, CreatedBy = 1 , Name = "Project 1"},
                new Project { Id = 2, CreatedBy = 2, Name = "Project 2"},
            },
        };
        var serviceCollection = new ServiceCollection();
        UserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic projects = res.Data["projects"];
        Assert.Equal(2, projects.Count);
        Assert.Equal("Name_1", projects[0].createdByName);
        Assert.Equal("Name_2", projects[1].createdByName);
    }

    [Fact]
    public void TestServicesBulkResolverList()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema.UpdateType<Project>(type =>
        {
            type.AddField("assignedUsers", "Get users assigned to project")
                .ResolveWithService<UserService>((proj, users) => users.GetUsersByProjectId(proj.Id, null))
                .ResolveBulk<UserService, int, List<User>>(proj => proj.Id, (ids, srv) => srv.GetUsersByProjectId(ids, null));
        });

        var gql = new QueryRequest
        {
            Query = @"{ 
                projects { 
                    name assignedUsers { name }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects = new List<Project> {
                new Project { Id = 1, CreatedBy = 1 , Name = "Project 1"},
                new Project { Id = 2, CreatedBy = 2, Name = "Project 2"},
            },
        };
        var serviceCollection = new ServiceCollection();
        UserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic projects = res.Data["projects"];
        Assert.Equal(2, projects.Count);
        Assert.Equal("Name_1", projects[0].assignedUsers[0].name);
        Assert.Equal("Name_2", projects[1].assignedUsers[0].name);
    }

    [Fact]
    public void TestServicesBulkResolverWithArg()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema.UpdateType<Project>(type =>
        {
            type.AddField("assignedUser", new { id = ArgumentHelper.Required<int>() }, "Get user assigned to project by ID")
                .ResolveWithService<UserService>((proj, args, users) => users.GetUserByProjectId(proj.Id, args.id))
                .ResolveBulk<UserService, int, User>(proj => proj.Id, (ids, args, srv) => srv.GetUserByIdForProjectId(ids, args.id));
        });

        var gql = new QueryRequest
        {
            Query = @"{ 
                projects { 
                    name assignedUser(id: 1) { name }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects = new List<Project> {
                new Project { Id = 1, CreatedBy = 1 , Name = "Project 1"},
                new Project { Id = 2, CreatedBy = 2, Name = "Project 2"},
            },
        };
        var serviceCollection = new ServiceCollection();
        UserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic projects = res.Data["projects"];
        Assert.Equal(2, projects.Count);
        Assert.Equal("Name_1", projects[0].assignedUser.name);
        Assert.Null(projects[1].assignedUser);
    }

    [Fact]
    public void TestServicesBulkResolverListWithArgs()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema.UpdateType<Project>(type =>
        {
            type.AddField("assignedUsers", new { name = (string)null }, "Get users assigned to project")
                .ResolveWithService<UserService>((proj, args, users) => users.GetUsersByProjectId(proj.Id, args.name))
                .ResolveBulk<UserService, int, List<User>>(proj => proj.Id, (ids, args, srv) => srv.GetUsersByProjectId(ids, args.name));
        });

        var gql = new QueryRequest
        {
            Query = @"{ 
                projects { 
                    name assignedUsers(name: ""1"") { name }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects = new List<Project> {
                new Project { Id = 1, CreatedBy = 1 , Name = "Project 1"},
                new Project { Id = 2, CreatedBy = 2, Name = "Project 2"},
            },
        };
        var serviceCollection = new ServiceCollection();
        UserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic projects = res.Data["projects"];
        Assert.Equal(2, projects.Count);
        Assert.Equal("Name_1", projects[0].assignedUsers[0].name);
        Assert.Empty(projects[1].assignedUsers);
    }
}