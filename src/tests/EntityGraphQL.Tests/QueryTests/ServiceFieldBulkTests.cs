using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
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
                .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedBy))
                .ResolveBulk<UserService, int, User>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsers(ids));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name createdBy { id field2 } 
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects = new List<Project>
            {
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
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
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects.Count);
        var project = projects[0];
        Assert.Equal(2, project.createdBy.GetType().GetFields().Length);
        Assert.Equal(1, project.createdBy.id);
        Assert.Equal("Hello", project.createdBy.field2);
    }

    [Fact]
    public void TestServicesBulkResolverWithinToSingle()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedBy))
                .ResolveBulk<UserService, int, User>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsers(ids));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                project(id: 1) { 
                    name createdBy { id field2 } 
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
            ],
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
        dynamic project = res.Data!["project"]!;
        Assert.Equal(2, project.createdBy.GetType().GetFields().Length);
        Assert.Equal(1, project.createdBy.id);
        Assert.Equal("SingleCall", project.createdBy.field2);
    }

    [Fact]
    public void TestServicesBulkResolverFullObjectWithOrderBy()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Query()
            .ReplaceField(
                "projects",
                new { like = (string?)null, },
                (ctx, args) => ctx.QueryableProjects.WhereWhen(f => f.Name.ToLower().Contains(args.like!.ToLower()), !string.IsNullOrEmpty(args.like)).OrderBy(f => f.Name),
                "Get projects"
            );
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedBy))
                .ResolveBulk<UserService, int, User>(proj => proj.Id, (ids, srv) => srv.GetUsersByProjectId(ids));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    id
                    name createdBy { id field2 } 
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
            ],
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
        dynamic projects = res.Data!["projects"]!;
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
                .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedBy).Name)
                .ResolveBulk<UserService, int, string>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsers(ids).ToDictionary(u => u.Key, u => u.Value.Name));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name createdByName
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
            ],
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
        dynamic projects = res.Data!["projects"]!;
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
                .Resolve<UserService>((proj, users) => users.GetUsersByProjectId(proj.Id, null))
                .ResolveBulk<UserService, int, List<User>>(proj => proj.Id, (ids, srv) => srv.GetUsersByProjectId(ids, null));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name assignedUsers { name }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
            ],
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
        dynamic projects = res.Data!["projects"]!;
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
                .Resolve<UserService>((proj, args, users) => users.GetUserByProjectId(proj.Id, args.id))
                .ResolveBulk<UserService, int, User?>(proj => proj.Id, (ids, args, srv) => srv.GetUserByIdForProjectId(ids, args.id));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name assignedUser(id: 1) { name }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
            ],
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
        dynamic projects = res.Data!["projects"]!;
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
            type.AddField("assignedUsers", new { name = (string?)null }, "Get users assigned to project")
                .Resolve<UserService>((proj, args, users) => users.GetUsersByProjectId(proj.Id, args.name))
                .ResolveBulk<UserService, int, List<User>>(proj => proj.Id, (ids, args, srv) => srv.GetUsersByProjectId(ids, args.name));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    name assignedUsers(name: ""1"") { name }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
            ],
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
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects.Count);
        Assert.Equal("Name_1", projects[0].assignedUsers[0].name);
        Assert.Empty(projects[1].assignedUsers);
    }

    [Fact]
    public void TestServicesBulkResolverFullObjectDeep()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Task>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .Resolve<UserService>((task, users) => users.GetUserById(task.Id))
                .ResolveBulk<UserService, int, User>(task => task.Id, (ids, srv) => srv.GetUsersByProjectId(ids));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    id
                    tasks {
                        name createdBy { id field2 } 
                    }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new()
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1",
                    Tasks = [new() { Id = 1, Name = "Task 1" }, new() { Id = 2, Name = "Task 2" },]
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2",
                    Tasks = [new() { Id = 3, Name = "Task 3" }, new() { Id = 4, Name = "Task 4" },]
                }
            ]
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
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects.Count);
        var project = projects[0];
        Assert.Equal(2, project.tasks.Count);
    }

    [Fact]
    public void TestServicesBulkResolverFullObjectDeep_Object_List_Object_List()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .Resolve<UserService>((project, users) => users.GetUserById(project.CreatedBy))
                .ResolveBulk<UserService, int, User>(project => project.CreatedBy, (ids, srv) => srv.GetUsersByProjectId(ids));
        });

        // select a single top level -> list -> single -> list -> service field
        //                   project -> tasks -> assignee -> projects -> createdBy
        var gql = new QueryRequest
        {
            Query =
                @"{ 
                project(id: 1) { 
                    id
                    tasks {
                        assignee {
                            projects {
                                createdBy { id field2 } 
                            }
                        }
                    }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new()
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1",
                    Tasks =
                    [
                        new()
                        {
                            Id = 1,
                            Name = "Task 1",
                            Assignee = new Person { Id = 1 }
                        },
                        new()
                        {
                            Id = 2,
                            Name = "Task 2",
                            Assignee = new Person { Id = 2 }
                        },
                    ]
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 1,
                    Name = "Project 2",
                    Tasks =
                    [
                        new()
                        {
                            Id = 3,
                            Name = "Task 3",
                            Assignee = new Person { Id = 3 }
                        },
                        new()
                        {
                            Id = 4,
                            Name = "Task 4",
                            Assignee = new Person { Id = 1 }
                        },
                    ]
                }
            ]
        };
        // set up fake data with no null paths (normally this is done with EF and the null paths are handled by the compiler)
        context.Projects[0].Tasks.ElementAt(0).Assignee!.Projects = context.Projects;
        context.Projects[0].Tasks.ElementAt(1).Assignee!.Projects = [];

        var serviceCollection = new ServiceCollection();
        UserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic project = res.Data!["project"]!;
        Assert.Equal(1, project.id);
        Assert.Equal(2, project.tasks.Count);
        Assert.Equal(2, project.tasks[0].assignee.projects.Count);
        Assert.Equal(0, project.tasks[1].assignee.projects.Count);
    }

    [Fact]
    public void TestServicesBulkResolverFullObjectDeep_Object_List_Object_List_HandlesNulls()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .Resolve<UserService>((project, users) => users.GetUserById(project.CreatedBy))
                .ResolveBulk<UserService, int, User>(project => project.CreatedBy, (ids, srv) => srv.GetUsersByProjectId(ids));
        });

        // select a single top level -> list -> single -> list -> service field
        //                   project -> tasks -> assignee -> projects -> createdBy
        var gql = new QueryRequest
        {
            Query =
                @"{ 
                project(id: 1) { 
                    id
                    tasks {
                        assignee {
                            projects {
                                createdBy { id field2 } 
                            }
                        }
                    }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new()
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1",
                    Tasks = [new() { Id = 1, Name = "Task 1" }, new() { Id = 2, Name = "Task 2" },]
                },
                new()
                {
                    Id = 2,
                    CreatedBy = 1,
                    Name = "Project 2",
                    Tasks = [new() { Id = 3, Name = "Task 3" }, new() { Id = 4, Name = "Task 4" },]
                }
            ]
        };
        // assignee is null in the data - bulk selector should handle this

        var serviceCollection = new ServiceCollection();
        UserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic project = res.Data!["project"]!;
        Assert.Equal(1, project.id);
        Assert.Equal(2, project.tasks.Count);
        Assert.Null(project.tasks[0].assignee);
    }

    [Fact]
    public void TestServicesBulkResolverFullObjectDeep_List_List_Object()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.UpdateType<Person>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .Resolve<UserService>((person, users) => users.GetUserById(person.Id))
                .ResolveBulk<UserService, int, User>(person => person.Id, (ids, srv) => srv.GetAllUsers(ids));
        });

        // select a list top level -> list -> single -> service field
        //                projects -> tasks -> assignee -> createdBy
        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    id
                    tasks {
                        assignee {
                            createdBy { id field2 } 
                        }
                    }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new()
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1",
                    Tasks =
                    [
                        new()
                        {
                            Id = 1,
                            Name = "Task 1",
                            Assignee = new Person { Id = 1 }
                        },
                        new()
                        {
                            Id = 2,
                            Name = "Task 2",
                            Assignee = new Person { Id = 2 }
                        },
                    ]
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 1,
                    Name = "Project 2",
                    Tasks =
                    [
                        new()
                        {
                            Id = 3,
                            Name = "Task 3",
                            Assignee = new Person { Id = 3 }
                        },
                        new()
                        {
                            Id = 4,
                            Name = "Task 4",
                            Assignee = new Person { Id = 1 }
                        },
                    ]
                }
            ]
        };

        // set up fake data with no null paths (normally this is done with EF and the null paths are handled by the compiler)
        context.Projects[0].Tasks.ElementAt(0).Assignee!.Projects = context.Projects;
        context.Projects[0].Tasks.ElementAt(1).Assignee!.Projects = [];

        var serviceCollection = new ServiceCollection();
        UserService userService = new();
        serviceCollection.AddSingleton(userService);
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(gql, sp, null);
        Assert.Null(res.Errors);
        // called once not for each project
        Assert.Equal(1, userService.CallCount);
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects.Count);
        var project = projects[0];
        Assert.Equal(2, project.tasks.Count);
        Assert.Equal(1, project.tasks[0].assignee.createdBy.id);
        Assert.Equal(2, project.tasks[1].assignee.createdBy.id);

        Assert.Equal("Hello", project.tasks[0].assignee.createdBy.field2);
    }

    [Fact]
    public void TestServicesBulkResolverFullObjectWithConnectionPaging()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().GetField(ctx => ctx.Projects).UseConnectionPaging();
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedBy))
                .ResolveBulk<UserService, int, User>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsers(ids));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    edges {
                        node {
                            name createdBy { id field2 } 
                        }
                    }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
            ],
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
        Assert.Equal(nameof(UserService.GetAllUsers), userService.Calls.First());
    }

    [Fact]
    public void TestServicesBulkResolverFullObjectWithOffsetPaging()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().GetField(ctx => ctx.Projects).UseOffsetPaging();
        schema.UpdateType<Project>(type =>
        {
            type.ReplaceField("createdBy", "Get user that created it")
                .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedBy))
                .ResolveBulk<UserService, int, User>(proj => proj.CreatedBy, (ids, srv) => srv.GetAllUsers(ids));
        });

        var gql = new QueryRequest
        {
            Query =
                @"{ 
                projects { 
                    items {
                        name createdBy { id field2 } 
                    }
                } 
            }"
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    CreatedBy = 1,
                    Name = "Project 1"
                },
                new Project
                {
                    Id = 2,
                    CreatedBy = 2,
                    Name = "Project 2"
                },
            ],
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
        Assert.Equal(nameof(UserService.GetAllUsers), userService.Calls.First());
    }
}
