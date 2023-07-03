using Xunit;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System;
using EntityGraphQL.Extensions;
using EntityGraphQL.Compiler;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EntityGraphQL.Tests
{
    public class ServiceFieldTests
    {
        [Fact]
        public void TestServicesAtRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Query().ReplaceField("projects",
                new PagerArgs { page = 1, pagesize = 10 },
                "Pagination. [defaults: page = 1, pagesize = 10]")
                .ResolveWithService<EntityPager>((db, p, pager) => pager.PageProjects(db, p));

            var gql = new QueryRequest
            {
                Query = @"{ projects { total items { id } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };
            var serviceCollection = new ServiceCollection();
            var pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
        }
        [Fact]
        public void TestServicesAtRoot2()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination", "Desc").AddAllFields();

            schema.Query().ReplaceField("projects",
                new PagerArgs { page = 1, pagesize = 10 },
                "Pagination. [defaults: page = 1, pagesize = 10]")
                .ResolveWithService<EntityPager>((db, p, pager) => pager.PageProjects(db.Projects, p))
                .Returns("ProjectPagination");

            var gql = new QueryRequest
            {
                Query = @"{ projects { total items { id } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };
            var serviceCollection = new ServiceCollection();
            var pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
        }

        [Fact]
        public void TestServicesNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                "Pagination. [defaults: page = 1, pagesize = 10]")
                .ResolveWithService<EntityPager>((person, args, pager) => pager.PageProjects(person.Projects, args));

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { total items { id } } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                {
                    new Person
                    {
                        Projects = new List<Project>()
                    }
                }
            };
            var serviceCollection = new ServiceCollection();
            var pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            // Builds - (ctx, pager, args) => ctx.People
            //              .Select(p => new { projects = p.Projects })
            //              .ToList()
            //              .Select(p => new { pager.PageProjects(p.projects, args) })

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            dynamic person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Single(person.GetType().GetFields());
            Assert.NotNull(person.GetType().GetField("projects"));
        }

        [Fact]
        public void TestServicesNonRootDeeper()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id));

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { 
                    config { type } 
                } } }"
            };

            var context = new TestDataContext
            {
                People = new List<Person>
                        {
                            new Person
                            {
                                Projects = new List<Project>
                                {
                                    new Project
                                    {
                                        Id = 4,
                                    }
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            // Builds - (ctx, srv, args) => ctx.People
            //              .Select(p => new {
            //                  projects = p.Projects.Select(p => new {
            //                      Id = p.Id
            //                  })
            //              })
            //              .ToList()
            //              .Select(p => new {
            //                  projects = p.Projects.Select(p => new {
            //                      config = wrapservice...(p.Id)
            //                  })
            //              })

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Single(person.GetType().GetFields());
            Assert.NotNull(person.GetType().GetField("projects"));
            dynamic project = Enumerable.ElementAt(person.projects, 0);
            Assert.NotNull(project.GetType().GetField("config"));
            Assert.Equal(1, srv.CallCount);
        }

        [Fact]
        public void TestServicesNonRootDeeperWithFullEntitySelect()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p));

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { 
                    config { type } 
                } } }"
            };

            var context = new TestDataContext
            {
                People = new List<Person>
                        {
                            new Person
                            {
                                Projects = new List<Project>
                                {
                                    new Project
                                    {
                                        Id = 4,
                                    }
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Single(person.GetType().GetFields());
            Assert.NotNull(person.GetType().GetField("projects"));
            dynamic project = Enumerable.ElementAt(person.projects, 0);
            Assert.NotNull(project.GetType().GetField("config"));
            Assert.Equal(1, srv.CallCount);
        }

        [Fact]
        public void TestServicesNonRootWithOtherFields()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                "Pagination. [defaults: page = 1, pagesize = 10]")
                .ResolveWithService<EntityPager>((person, p, pager) => pager.PageProjects(person.Projects, p));

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { total items { id } } name } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Name = "Alyssa",
                                Projects = new List<Project>()
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            EntityPager pager = new();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("projects"));
            Assert.NotNull(person.GetType().GetField("name"));
        }

        [Fact]
        public void TestServicesNonRootWithOtherFieldsAndRelation()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                "Pagination. [defaults: page = 1, pagesize = 10]")
                .ResolveWithService<EntityPager>((person, p, pager) => pager.PageProjects(person.Projects, p));

            var gql = new QueryRequest
            {
                Query = @"{ people { projects { total items { id } } manager { name } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Name = "Alyssa",
                                Projects = new List<Project>(),
                                Manager = new Person
                                {
                                    Name = "Jennifer"
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            EntityPager pager = new();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("projects"));
            Assert.NotNull(person.GetType().GetField("manager"));
        }

        [Fact]
        public void TestServicesNonRootWithOtherFieldsAndRelation2()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().AddField("age", "Persons age")
                .ResolveWithService<AgeService>(
                    // use a filed not another relation/entity
                    (person, ager) => ager.GetAge(person.Birthday)
                );

            var gql = new QueryRequest
            {
                Query = @"{ people { age manager { name } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Name = "Alyssa",
                                Projects = new List<Project>(),
                                Manager = new Person
                                {
                                    Name = "Jennifer"
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            var ager = new AgeService();
            serviceCollection.AddSingleton(ager);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, ager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("age"));
            Assert.NotNull(person.GetType().GetField("manager"));
        }

        [Fact]
        public void TestServicesNonRootWithOtherFieldsAndRelationNotEnumerable()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().AddField("age", "Persons age")
                .ResolveWithService<AgeService>(
                    // use a filed not another relation/entity
                    (person, ager) => ager.GetAge(person.Birthday)
                );

            var gql = new QueryRequest
            {
                // the service field (age) is on a 1-1 relation not 1-Many so we don't build a .Select
                Query = @"{ people { manager { name age } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Name = "Alyssa",
                                Projects = new List<Project>(),
                                Manager = new Person
                                {
                                    Name = "Jennifer"
                                }
                            }
                        }
            };
            var serviceCollection = new ServiceCollection();
            var ager = new AgeService();
            serviceCollection.AddSingleton(ager);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, ager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Type resultType = person.GetType();
            Assert.Single(resultType.GetFields());
            Assert.Equal("manager", resultType.GetFields()[0].Name);
            Assert.NotNull(person.manager.GetType().GetField("name"));
            Assert.NotNull(person.manager.GetType().GetField("age"));

        }

        [Fact]
        public void TestServicesReconnectToSchemaContext()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            // Linking a type from a service back to the schema context
            schema.Type<User>().ReplaceField("projects", "Peoples projects")
                .ResolveWithService<TestDataContext>((user, db) => db.Projects.Where(p => p.Owner.Id == user.Id));

            schema.Query().ReplaceField("user", "Get current user")
                .ResolveWithService<UserService>((ctx, users) => users.GetUser());

            var gql = new QueryRequest
            {
                Query = @"{ user { id projects { id } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Projects = new List<Project>()
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            UserService userService = new();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            Assert.Equal(2, res.Data["user"].GetType().GetFields().Length);
            Assert.Equal("id", res.Data["user"].GetType().GetFields().ElementAt(0).Name);
            Assert.Equal("projects", res.Data["user"].GetType().GetFields().ElementAt(1).Name);
        }

        [Fact]
        public void TestServicesReconnectToSchemaContextListOf()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            // Linking a type from a service back to the schema context
            schema.Type<User>().ReplaceField("projects", "Peoples projects")
                .ResolveWithService<TestDataContext>((user, db) => db.Projects.Where(p => p.Owner.Id == user.Id));

            schema.Query().ReplaceField("users", "Get current user")
                .ResolveWithService<UserService>((ctx, users) => users.GetUsers(3));

            var gql = new QueryRequest
            {
                Query = @"{ users { id projects { id } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                        {
                            new Person
                            {
                                Projects = new List<Project>()
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            UserService userService = new();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            dynamic users = res.Data["users"];
            Assert.Equal(2, users[0].GetType().GetFields().Length);
            Assert.Equal("id", Enumerable.ElementAt(users[0].GetType().GetFields(), 0).Name);
            Assert.Equal("projects", Enumerable.ElementAt(users[0].GetType().GetFields(), 1).Name);
        }

        [Fact]
        public void TestServicesReconnectToSchemaContextFromObject()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            // Linking a type from a service back to the schema context
            schema.Type<User>().ReplaceField("project", "Peoples project")
                .ResolveWithService<TestDataContext>((user, db) => db.Projects.FirstOrDefault(p => p.Owner.Id == user.Id));

            schema.Query().ReplaceField("user", "Get current user")
                .ResolveWithService<UserService>((ctx, users) => users.GetUser());

            var gql = new QueryRequest
            {
                Query = @"{ user { id project { __typename id } } }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>(),
                People = new List<Person>
                {
                    new Person
                    {
                        Projects = new List<Project>()
                    }
                },
            };
            var serviceCollection = new ServiceCollection();
            UserService userService = new();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            dynamic user = res.Data["user"];
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.Equal("id", Enumerable.ElementAt(user.GetType().GetFields(), 0).Name);
            Assert.Equal("project", Enumerable.ElementAt(user.GetType().GetFields(), 1).Name);
        }

        [Fact]
        public void TestDbContextToServiceBackToDbContext()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            // Linking a type from a service back to the schema context
            schema.Type<User>().ReplaceField("projects", "Users projects")
                .ResolveWithService<TestDataContext>((user, db) => db.Projects.Where(p => p.CreatedBy == user.Id));

            schema.Type<Project>().ReplaceField("createdBy", "Get user that created the project")
                .ResolveWithService<UserService>((p, users) => users.GetUserById(p.CreatedBy));

            var gql = new QueryRequest
            {
                Query = @"{ 
                    project(id: 1) {
                        name
                        createdBy { 
                            id 
                            projects { id name }
                        }
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
                        Name = "Project 1",
                        CreatedBy = 1
                    }
                },
                People = new List<Person>
                {
                    new Person
                    {
                        Projects = new List<Project>()
                    }
                },
            };
            var serviceCollection = new ServiceCollection();
            UserService userService = new();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            dynamic project = res.Data["project"];
            Assert.Equal(2, project.GetType().GetFields().Length);
            Assert.Equal("name", Enumerable.ElementAt(project.GetType().GetFields(), 0).Name);
            Assert.Equal("createdBy", Enumerable.ElementAt(project.GetType().GetFields(), 1).Name);
        }

        [Fact]
        public void TestServicesMultipleReconnectToSchemaContextListOf_WithoutSelectionOfNeededDbField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            // root field is a service returning a list of items
            schema.Query().ReplaceField("users", "Get current user")
                .ResolveWithService<UserService>((_, users) => users.GetUsers(10));

            // connect back to a entity in the DB context
            schema.Type<User>().ReplaceField("projects", "Peoples projects")
                .ResolveWithService<TestDataContext>((user, db) => db.Projects.Where(p => p.Owner.Id == user.Id));

            schema.Type<User>().AddField("currentTask", "Peoples current task")
                .ResolveWithService<TestDataContext>((user, db) => db.Tasks.FirstOrDefault(t => t.Assignee.Id == user.Id));

            schema.Type<User>().AddField("indirectTasks", "Tasks assigned to people managed by user")
             .ResolveWithService<TestDataContext2>((user, db) =>
                 from task in db.Tasks
                 where task.Assignee.Manager == user.Relation
                 select task);

            schema.Type<User>().AddField("indirectTasksWithExplicitJoin", "Tasks assigned to people managed by user")
                .ResolveWithService<TestDataContext2>((user, db) =>
                    from task in db.Tasks
                    join person in db.People on task.Assignee equals person
                    where person.Manager == user.Relation
                    select task);

            var gql = new QueryRequest
            {
                Query = @"{ 
                    users {
                        # missing id but required for the projects/currentTask fields
                        field1 # from user
                        projects { name } 
                        currentTask { name }
                        indirectTasks { name }
                        indirectTasksWithExplicitJoin { name }
                    } 
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Name = "Project 1",
                        Owner = new Person
                        {
                            Id = 10,
                        }
                    }
                },
                Tasks = new List<Task>
                {
                    new Task
                    {
                        Name = "Task 1",
                        Assignee = new Person
                        {
                            Id = 10,
                        }
                    }
                }
            };
            var serviceCollection = new ServiceCollection();
            UserService userService = new();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);
            serviceCollection.AddSingleton(new TestDataContext2());

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            dynamic users = res.Data["users"];
            Assert.Equal(5, users[0].GetType().GetFields().Length);
            Assert.Equal("field1", Enumerable.ElementAt(users[0].GetType().GetFields(), 0).Name);
            Assert.Equal("projects", Enumerable.ElementAt(users[0].GetType().GetFields(), 1).Name);
            Assert.Equal("currentTask", Enumerable.ElementAt(users[0].GetType().GetFields(), 2).Name);
            dynamic user = users[0];
            Assert.Equal("Task 1", user.currentTask.name);
            Assert.Equal("Project 1", user.projects[0].name);
        }

        [Fact]
        public void TestSelectFromServiceDeepInLists()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Settings>("Settings", "The settings").AddAllFields();
            schema.Type<Task>().AddField("settings", "Task settings")
                .ResolveWithService<SettingsService>((t, settings) => settings.Get(t.Id, false))
                .IsNullable(false);

            var gql = new QueryRequest
            {
                Query = @"query {
          projects {
            tasks {
              settings {
                allowComments
              }
              id # the service field below requires id. Make sure we don't select it twice
            }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task> { new Task() }
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            var settings = new SettingsService();
            serviceCollection.AddSingleton(settings);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Null(res.Errors);
            Assert.Equal(1, settings.CallCount);
            Assert.Single(projectType.GetFields());
            Assert.Equal("tasks", projectType.GetFields()[0].Name);
            Assert.Equal("settings", project.tasks.GetType().GetGenericArguments()[0].GetFields()[0].Name);
            Assert.NotNull(project.tasks[0].settings.GetType().GetField("allowComments"));
        }

        [Fact]
        public void TestComplexFieldWithServiceField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Settings>("Settings", "The settings").AddAllFields();
            schema.Type<Project>().AddField("settings", "Project settings")
                .ResolveWithService<SettingsService>((t, settings) => settings.Get(t.Id, false))
                .IsNullable(false);
            schema.Type<Project>().AddField("totalTasks", p => p.Tasks.Count(), "Total tasks");

            var gql = new QueryRequest
            {
                Query = @"query {
                    projects {
                        totalTasks
                        settings {
                            allowComments
                        }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task> { new Task() }
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            var settings = new SettingsService();
            serviceCollection.AddSingleton(settings);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Null(res.Errors);
            Assert.Equal(1, settings.CallCount);
            Assert.Equal(2, projectType.GetFields().Length);
            Assert.Equal("totalTasks", projectType.GetFields()[0].Name);
            Assert.Equal("settings", projectType.GetFields()[1].Name);
            Assert.NotNull(project.settings.GetType().GetField("allowComments"));
        }

        [Fact]
        public void TestComplexFieldInObjectWithServiceField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Settings>("Settings", "The settings").AddAllFields();
            schema.Type<Project>().AddField("settings", "Project settings")
                .ResolveWithService<SettingsService>((t, settings) => settings.Get(t.Id, false))
                .IsNullable(false);
            schema.Type<Project>().AddField("totalTasks", p => p.Tasks.Count(), "Total tasks");
            schema.Type<Person>().AddField("managerId", p => p.Manager.Id, "Persons managers ID");

            var gql = new QueryRequest
            {
                Query = @"query {
          projects {
            owner {
                managerId
            }
            settings {
              allowComments
            }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task> { new Task() },
                                Owner = new Person
                                {
                                    Id = 77,
                                    Manager = new Person { Id = 99 }
                                }
                            }
                        },
            };
            var serviceCollection = new ServiceCollection();
            var settings = new SettingsService();
            serviceCollection.AddSingleton(settings);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(1, settings.CallCount);
            Assert.Equal(2, projectType.GetFields().Length);
            Assert.Equal("owner", projectType.GetFields()[0].Name);
            Assert.NotNull(project.owner.GetType().GetField("managerId"));
            Assert.Equal("settings", projectType.GetFields()[1].Name);
            Assert.NotNull(project.settings.GetType().GetField("allowComments"));
        }

        [Fact]
        public void TestServiceThenSubSelectWithWhere()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id));
            // because of the selections of the config.type (a service) when we do our second selection (first is EF compatible)
            // the type of t will not match, need to be replaced
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.Where(t => t.IsActive), "Active tasks");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
          projects {
            config { type }
            tasks { id }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task>
                                {
                                    new Task
                                    {
                                        Id = 98
                                    }
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(2, projectType.GetFields().Length);
            Assert.Equal("config", projectType.GetFields()[0].Name);
            Assert.Equal("tasks", projectType.GetFields()[1].Name);
        }

        [Fact]
        public void TestServiceThenSubSelectWithWhereAlreadySelectedFieldInWhere()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id));
            // because of the selections of the config.type (a service) when we do our second selection (first is EF compatible)
            // the type of t will not match, need to be replaced
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.Where(t => t.IsActive), "Active tasks");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
          projects {
            config { type }
            tasks { id isActive }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task>
                                {
                                    new Task { Id = 98 }
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(2, projectType.GetFields().Length);
            Assert.Equal("config", projectType.GetFields()[0].Name);
            Assert.Equal("tasks", projectType.GetFields()[1].Name);
        }

        [Fact]
        public void TestServiceThenSubSelectWithWhereDuplicateFieldNames()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id));
            // because of the selections of the config.type (a service) when we do our second selection (first is EF compatible)
            // the type of t will not match, need to be replaced
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.Where(t => t.IsActive), "Active tasks");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
          projects {
            config { type }
            tasks { isActive project { isActive } }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task>
                                {
                                    new Task { Id = 98, IsActive = true }
                                }
                            }
                        },
            };
            context.Projects.First().Tasks.First().Project = context.Projects.First();

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(2, projectType.GetFields().Length);
            Assert.Equal("config", projectType.GetFields()[0].Name);
            Assert.Equal("tasks", projectType.GetFields()[1].Name);
        }

        [Fact]
        public void TestServiceThenSubOrderBy()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id));
            // because of the selections of the config.type (a service) when we do our second selection (first is EF compatible)
            // the type of t will not match, need to be replaced
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.OrderBy(t => t.IsActive), "Ordered tasks");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
          projects {
            config { type }
            tasks { isActive }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = new List<Task>
                                {
                                    new Task { Id = 98, IsActive = true }
                                }
                            }
                        },
            };
            context.Projects.First().Tasks.First().Project = context.Projects.First();

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Equal(2, projectType.GetFields().Length);
            Assert.Equal("config", projectType.GetFields()[0].Name);
            Assert.Equal("tasks", projectType.GetFields()[1].Name);
        }

        [Fact]
        public void TestWhereWhenWithServiceField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("configType", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id).Type);

            schema.Query().ReplaceField("projects",
                new
                {
                    search = (string)null,
                },
                (ctx, args) => ctx.Projects
                    .WhereWhen(p => p.Description.ToLower().Contains(args.search), !string.IsNullOrEmpty(args.search))
                    .OrderBy(p => p.Description),
                "List of projects");

            var serviceCollection = new ServiceCollection();
            ConfigService srv = new();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
            projects {
                configType
            }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = null,
                                Description = "Hello"
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.First((dynamic)res.Data["projects"]);
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("configType", projectType.GetFields()[0].Name);
        }

        [Fact]
        public void TestWhereWhenWithServiceFieldAndArgument()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("configType", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(0).Type);

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
                    project(id: 0) {
                        configType
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Tasks = null,
                                Description = "Hello"
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            dynamic project = (dynamic)res.Data["project"];
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("configType", projectType.GetFields()[0].Name);
        }

        [Fact]
        public void TestCollectionToSingleWithServiceAndCollectionFragmentWithServiceAndSkippedRelation()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("configType", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(0).Type);

            schema.Type<Task>().AddField("assigneeProjects", t => t.Assignee.Projects, "All projects for assignee");
            schema.Type<Person>().AddField("age", "Persons age")
                .ResolveWithService<AgeService>(
                    // use a filed not another relation/entity
                    (person, ager) => ager.GetAge(person.Birthday)
                );

            var serviceCollection = new ServiceCollection();
            ConfigService configSrv = new();
            serviceCollection.AddSingleton(configSrv);
            AgeService ageSrv = new();
            serviceCollection.AddSingleton(ageSrv);

            var gql = new QueryRequest
            {
                Query = @"
        fragment taskFrag on Task {
            assigneeProjects { # skips relation in context
                id
            }
            assignee { # relation
                age # service
            }
        }

        query {
            project(id: 0) { # context collection to single
                description
                configType # service field
                tasks {
                    ...taskFrag
                }
            }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Id = 0,
                                Description = "Hello",
                                Tasks = new List<Task>
                                {
                                    new Task
                                    {
                                        Assignee = new Person
                                        {
                                            Name = "Billy",
                                            Projects = new List<Project>()
                                        }
                                    }
                                },
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            Assert.Equal(1, ageSrv.CallCount);
            dynamic project = res.Data["project"];
        }

        [Fact]
        public void TestCollectionToSingleWithServiceInCollection()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("configType", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(0).Type);

            var serviceCollection = new ServiceCollection();
            var configSrv = new ConfigService();
            serviceCollection.AddSingleton(configSrv);

            var gql = new QueryRequest
            {
                Query = @"query {
                    task(id: 1) { # context collection to single
                        project {
                            configType
                        }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                                Project = new Project
                                {
                                    Id = 0,
                                    Description = "Hello",
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            dynamic project = res.Data["task"];
        }

        [Fact]
        public void TestCollectionToSingleWithServiceTypeInCollection()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig", "Config for the project").AddAllFields();
            schema.Type<Project>().AddField("config", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(0));

            var serviceCollection = new ServiceCollection();
            var configSrv = new ConfigService();
            serviceCollection.AddSingleton(configSrv);

            var gql = new QueryRequest
            {
                Query = @"query {
            task(id: 1) { # context collection to single
                project {
                    config { # service
                        type
                    }
                }
            }
        }"
            };

            var context = new TestDataContext
            {
                Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                                Project = new Project
                                {
                                    Id = 0,
                                    Description = "Hello",
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            dynamic project = (dynamic)res.Data["task"];
        }

        [Fact]
        public void TestCollectionToSingleWithServiceTypeInCollectionThatUsesAContextField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig", "Config for the project").AddAllFields();
            schema.Type<Project>().AddField("config", "Get project config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id));

            var serviceCollection = new ServiceCollection();
            ConfigService configSrv = new();
            serviceCollection.AddSingleton(configSrv);

            var gql = new QueryRequest
            {
                Query = @"query {
            task(id: 1) { # context collection to single
                project {
                    config {
                        type
                    }
                }
            }
        }"
            };

            var context = new TestDataContext
            {
                Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                                Project = new Project
                                {
                                    Id = 0,
                                    Description = "Hello",
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            dynamic project = (dynamic)res.Data["task"];
        }

        [Fact]
        public void TestCollectionToSingle_ToObjectRelation_WithAsyncServiceArrayField_UsingContextFieldWithMemberCall()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("arrayField", "Get project config")
                // p.Updated.HasValue is the important bit here
                .ResolveWithService<ConfigService>((p, x) => x.GetArrayFieldAsync(p.Id, p.Updated.HasValue).GetAwaiter().GetResult())
                .IsNullable(false);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ConfigService, ConfigService>();

            var gql = new QueryRequest
            {
                Query = @"{
                    task(id: 1) {
                        project {
                            arrayField
                        }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                                Project = new Project
                                {
                                    Id = 0,
                                    Description = "Hello",
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var project = (dynamic)res.Data["task"];
        }

        [Fact]
        public void TestCollectionToSingle_WithAsyncService_UsingContextFieldEnumerable()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().AddField("serviceField", "Get project config")
                // p.Updated.HasValue is the important bit here
                .ResolveWithService<ConfigService>((p, x) => x.GetFieldAsync(p.Id, p.Tasks.Where(t => t.Name != "Task").Select(t => t.Id)).GetAwaiter().GetResult());

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddScoped<ConfigService, ConfigService>();

            var gql = new QueryRequest
            {
                Query = @"{
                    project(id: 1) {
                        serviceField
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
                                Tasks = new List<Task>
                                {
                                    new Task {
                                        Id = 0,
                                    }
                                }
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var project = (dynamic)res.Data["project"];
        }

        [Fact]
        public void TestServiceAfterMultipleCollectionToSingle()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig", "Config for the task").AddAllFields();
            schema.Type<Task>().AddField("config", "Get task config")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id));
            schema.Type<Project>().AddField("firstTask",
                (p) => p.Tasks.FirstOrDefault(),
                "Get first task");

            var serviceCollection = new ServiceCollection();
            ConfigService configSrv = new();
            serviceCollection.AddSingleton(configSrv);

            var gql = new QueryRequest
            {
                Query = @"query {
                    project(id: 0) { # context collection to single
                        firstTask { # still EF context but uses first()
                            config { #service
                                type
                            }
                        }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 0,
                        Description = "Hello",
                        Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                            }
                        },
                    }
                }
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, configSrv.CallCount);
            dynamic project = (dynamic)res.Data["project"];
        }

        [Fact]
        public void TestCollectionCollectionObjectThenService()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Person>().AddField("age", "Get age")
                .ResolveWithService<AgeService>((p, srv) => srv.GetAge(p.Birthday));

            var serviceCollection = new ServiceCollection();
            AgeService service = new();
            serviceCollection.AddSingleton(service);

            var gql = new QueryRequest
            {
                Query = @"query {
                    projects {
                        tasks {
                            assignee {
                                age
                                __typename
                            }
                        }
                    }
                }"
            };

            var context = new TestDataContext();
            context.FillWithTestData();
            context.Projects.ElementAt(0).Tasks.ElementAt(0).Assignee = new Person
            {
                Birthday = new DateTime(1990, 9, 3),
                Name = "yo"
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, service.CallCount);
            dynamic projects = res.Data["projects"];
            Assert.Equal(2, projects[0].tasks[0].assignee.GetType().GetFields().Length);
        }

        [Fact]
        public void TestCollectionWithService()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();
            schema.Query().ReplaceField("people", new
            {
                height = (int?)null
            },
            (ctx, args) => ctx.People
                .WhereWhen(p => p.Height > args.height, args.height.HasValue)
                .OrderBy(p => p.Height),
            "Get people with height > {height}");
            schema.Type<Person>().AddField("resolvedSettings", "Return resolved settings")
                .ResolveWithService<ConfigService>((f, b) => b.Get(f.Id))
                .IsNullable(false);

            var serviceCollection = new ServiceCollection();
            ConfigService service = new();
            serviceCollection.AddSingleton(service);

            var gql = new QueryRequest
            {
                Query = @"query OpName {
                    people {
                        resolvedSettings {
                            type
                        }
                    }
                }"
            };

            var context = new TestDataContext();
            context.FillWithTestData();

            var doc = new GraphQLCompiler(schema).Compile(gql, new QueryRequestContext(null, null));

            Assert.Single(doc.Operations);
            Assert.Single(doc.Operations[0].QueryFields);
        }
        [Fact]
        public void TestRootServiceFieldBackToContext()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().AddField("currentUser", "Returns current user")
                .ResolveWithService<UserService>((ctx, srv) => srv.GetUserById(ctx.People.FirstOrDefault().Id));

            schema.UpdateType<User>(type =>
                type.AddField("projectNames", "Get project names")
                .ResolveWithService<TestDataContext>((user, ctx) => ctx.Projects.Select(u => u.CreatedBy == user.Id)));
            var gql = new QueryRequest
            {
                Query = @"query {
                    currentUser {
                        projectNames
                    }
                }"
            };

            var context = new TestDataContext();
            context.People.Clear();
            context.People.Add(new Person
            {
                Projects = new List<Project>()
            });

            var serviceCollection = new ServiceCollection();
            AgeService service = new();
            serviceCollection.AddSingleton(service);

            // what we want to test here is that ctx.People.FirstOrDefault().Id is pulled up into the pre-services expression
            var graphQLCompiler = new GraphQLCompiler(schema);
            var compiledQuery = graphQLCompiler.Compile(gql, new QueryRequestContext(null, null));
            var query = compiledQuery.Operations[0];
            var node = query.QueryFields[0];

            // first stage without services
            var expression = node.GetNodeExpression(new CompileContext(), serviceCollection.BuildServiceProvider(), new List<GraphQLFragmentStatement>(), query.OpVariableParameter, null, Expression.Parameter(typeof(TestDataContext)), withoutServiceFields: true, null, isRoot: true, false, new Compiler.Util.ParameterReplacer());

            Assert.NotNull(expression);
            Assert.Equal("ctx.People.FirstOrDefault().Id", expression.ToString());
        }

        [Fact]
        public void TestServiceFieldNullableCheckOnChildObject()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Query().ReplaceField("users", "Get current user")
                .ResolveWithService<UserService>((ctx, users) => users.GetUsers(3));

            // join back to DB Context
            schema.UpdateType<User>(userSchema =>
                userSchema.ReplaceField("relation", "Get relation")
                    // We want the expression to return null
                    .ResolveWithService<TestDataContext>((user, ctx) => user.RelationId.HasValue ? ctx.People.FirstOrDefault(u => u.Id == user.RelationId.Value) : null)
            );

            var gql = new QueryRequest
            {
                Query = @"{ 
                    users { 
                        field2 
                        relation { id } 
                    }
                }"
            };

            var context = new TestDataContext();
            var serviceCollection = new ServiceCollection();
            UserService userService = new();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            dynamic users = res.Data["users"];
            Assert.Equal(2, users[0].GetType().GetFields().Length);
            Assert.Equal("field2", Enumerable.ElementAt(users[0].GetType().GetFields(), 0).Name);
            Assert.Equal("relation", Enumerable.ElementAt(users[0].GetType().GetFields(), 1).Name);
        }

        [Fact]
        public void TestServiceFieldReturnsNullList()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("configs", "Get project configs if they exists")
                .ResolveWithService<ConfigService>((p, srv) => srv.GetNullList(p.Id));

            schema.Query().ReplaceField("project",
                new
                {
                    id = (int?)null,
                    search = (string)null,
                },
                (ctx, args) => ctx.Projects
                    .WhereWhen(c => c.Id == args.id, args.id != null)
                    .WhereWhen(p => p.Description.ToLower().Contains(args.search), !string.IsNullOrEmpty(args.search))
                    .SingleOrDefault(),
                "project details");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
                    project(id: 0) {
                        configs { type }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 0,
                    }
                },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var project = (dynamic)res.Data["project"];
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("configs", projectType.GetFields()[0].Name);
            Assert.Null(project.configs);
            Assert.Equal(1, srv.CallCount);
        }

        [Fact]
        public void TestServiceFieldReturnsList()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("configs", "Get project configs if they exists")
                .ResolveWithService<ConfigService>((p, srv) => srv.GetList(p.Id))
                .IsNullable(false);

            schema.Query().ReplaceField("project",
                new
                {
                    id = (int?)null,
                    search = (string)null,
                },
                (ctx, args) => ctx.Projects
                    .WhereWhen(c => c.Id == args.id, args.id != null)
                    .WhereWhen(p => p.Description.ToLower().Contains(args.search), !string.IsNullOrEmpty(args.search))
                    .SingleOrDefault(),
                "project details");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
                    project(id: 0) {
                        configs { type }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                        {
                            new Project
                            {
                                Id = 0,
                            }
                        },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var project = (dynamic)res.Data["project"];
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("configs", projectType.GetFields()[0].Name);
            Assert.Empty(project.configs);
            // null check should not cause multiple calls
            Assert.Equal(1, srv.CallCount);
        }

        [Fact]
        public void TestServiceFieldWithQueryable()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project configs if they exists")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id))
                .IsNullable(false);
            schema.Type<Project>().ReplaceField("task", p => p.Tasks.FirstOrDefault(), "A task");

            schema.Query().ReplaceField("projects",
                new
                {
                    id = (int?)null,
                    search = (string)null,
                },
                (ctx, args) => ctx.QueryableProjects
                    .WhereWhen(c => c.Id == args.id, args.id != null)
                    .WhereWhen(p => p.Description.ToLower().Contains(args.search), !string.IsNullOrEmpty(args.search))
                    .OrderBy(c => c.Id)
                    .ThenBy(p => p.Description),
                "projects");

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
                    projects {
                        config { type __typename }
                        task {
                            id
                        }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 0,
                        Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                            }
                        }
                    }
                },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var projects = (dynamic)res.Data["projects"];
            Type projectType = Enumerable.First(projects).GetType();
            Assert.Equal(2, projectType.GetFields().Length);
            Assert.Equal("config", projectType.GetFields()[0].Name);
            // null check should not cause multiple calls
            Assert.Equal(1, srv.CallCount);
        }

        [Fact]
        public void TestServiceFieldWithStaticMethod()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project configs if they exists")
                .ResolveWithService<ConfigService>((p, srv) => GetProjectConfig(p, srv))
                .IsNullable(false);

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
                    projects {
                        config { type __typename }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 0,
                        Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                            }
                        }
                    }
                },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var projects = (dynamic)res.Data["projects"];
            Type projectType = Enumerable.First(projects).GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("config", projectType.GetFields()[0].Name);
        }

        [Fact]
        public void TestServiceFieldWithInstanceMethod()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("config", "Get project configs if they exists")
                .ResolveWithService<ConfigService>((p, srv) => GetProjectConfigInstance(p, srv))
                .IsNullable(false);

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
                    projects {
                        config { type __typename }
                    }
                }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 0,
                        Tasks = new List<Task>
                        {
                            new Task
                            {
                                Id = 1,
                            }
                        }
                    }
                },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var projects = (dynamic)res.Data["projects"];
            Type projectType = Enumerable.First(projects).GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("config", projectType.GetFields()[0].Name);
        }

        private static ProjectConfig GetProjectConfig(Project project, ConfigService configService) => configService.Get(project.Id);
        private ProjectConfig GetProjectConfigInstance(Project project, ConfigService configService) => configService.Get(project.Id);

        [Fact]
        public void TestScalarServiceFieldWithSameTypeReferencedExpression()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("someService", "Get project configs if they exists")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Name, p.Children.FirstOrDefault().Name));

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
                     projects {
                         someService 
                     }
                 }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                 {
                     new Project
                     {
                         Id = 0,
                         Name = "Project",
                         Children = new List<Project>
                         {
                             new Project
                             {
                                 Id = 1,
                                 Name = "Child1"
                             },
                             new Project
                             {
                                 Id = 2,
                                 Name = "Child2"
                             }
                         },
                     }
             },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var projects = (dynamic)res.Data["projects"];
            Type projectType = Enumerable.First(projects).GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("someService", projectType.GetFields()[0].Name);
            Assert.Equal(1, srv.CallCount);
            Assert.Single(projects);
            Assert.Equal("ProjectChild1", projects[0].someService);
        }

        [Fact]
        public void TestServiceFieldWithExpressionReturnCollection()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();

            schema.Type<Project>().AddField("someService", "Get project configs if they exists")
                .ResolveWithService<ConfigService>((p, srv) => srv.GetCollection(p.Name, p.Children.FirstOrDefault().Name));

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);

            var gql = new QueryRequest
            {
                Query = @"{
                     projects {
                         someService { id }
                     }
                 }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>
                 {
                     new Project
                     {
                         Id = 0,
                         Name = "Project",
                         Children = new List<Project>
                         {
                             new Project
                             {
                                 Id = 1,
                                 Name = "Child1"
                             },
                             new Project
                             {
                                 Id = 2,
                                 Name = "Child2"
                             }
                         },
                     }
             },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            var projects = (dynamic)res.Data["projects"];
            Type projectType = Enumerable.First(projects).GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("someService", projectType.GetFields()[0].Name);
            Assert.Equal(1, srv.CallCount);
            Assert.Single(projects);
            Assert.Equal(3, projects[0].someService.Count);
        }

        public class ConfigService
        {
            public ConfigService()
            {
                CallCount = 0;
            }

            public int CallCount { get; set; }

            public ProjectConfig Get(Project p) => Get(p.Id);

            public ProjectConfig Get(int id)
            {
                CallCount += 1;
                return new ProjectConfig
                {
                    Type = "Something"
                };
            }

            public string Get(string str1, string str2)
            {
                CallCount += 1;
                return str1 + str2;
            }

            public IEnumerable<Project> GetCollection(string str1, string str2)
            {
                CallCount += 1;
                return new List<Project>{
                     new Project {
                         Id = 1,
                         Name = str1
                     },
                     new Project {
                         Id = 2,
                         Name = str2
                     },
                     new Project {
                         Id = 3,
                         Name = str1 + str2
                     }
                 };
            }

            public ProjectConfig[] GetNullList(int id)
            {
                CallCount += 1;
                return null;
            }
            public ProjectConfig[] GetList(int id)
            {
                CallCount += 1;
                return Array.Empty<ProjectConfig>();
            }

            public int GetHalfInt(int value)
            {
                CallCount += 1;
                return value / 2;
            }

            internal Task<int[]> GetArrayFieldAsync(int id, bool code)
            {
                CallCount += 1;
                return System.Threading.Tasks.Task.FromResult(new int[] { id });
            }

            internal Task<int> GetFieldAsync(int id, IEnumerable<int> enumerable)
            {
                CallCount += 1;
                return System.Threading.Tasks.Task.FromResult(id);
            }
        }

        public class EntityPager
        {
            public EntityPager()
            {
                CallCount = 0;
            }

            public int CallCount { get; private set; }
            public Pagination<Project> PageProjects(TestDataContext db, dynamic arg)
            {
                CallCount += 1;
                int page = (int)arg.page;
                int pagesize = (int)arg.pagesize;

                //Pagination
                int total = db.Projects.Count();
                int pagecount = total + pagesize / pagesize;
                int skipTo = (page * pagesize) - pagesize;

                //Data
                var projects = db.Projects
                    .Skip(skipTo)
                    .Take(pagesize);

                return new Pagination<Project> { Total = total, PageCount = pagecount, Items = projects };
            }
            public Pagination<Project> PageProjects(IEnumerable<Project> projects, dynamic arg)
            {
                CallCount += 1;
                int page = (int)arg.page;
                int pagesize = (int)arg.pagesize;

                //Pagination
                int total = projects.Count();
                int pagecount = total + pagesize / pagesize;
                int skipTo = (page * pagesize) - pagesize;

                //Data
                var newProjects = projects
                    .Skip(skipTo)
                    .Take(pagesize);

                return new Pagination<Project> { Total = total, PageCount = pagecount, Items = newProjects };
            }
        }


        public class Pagination<TEntity>
        {
            [GraphQLNotNull]
            public IEnumerable<TEntity> Items { get; set; }
            [GraphQLNotNull]
            public int Total { get; set; }
            [GraphQLNotNull]

            public int PageCount { get; set; }
        }
    }

    public class SettingsService
    {
        public int CallCount { get; internal set; }

        public Settings Get(int id, bool someBool)
        {
            CallCount += 1;
            return new Settings();
        }
    }

    public class Settings
    {
        public bool AllowComments { get; set; } = false;
    }

    internal class PagerArgs
    {
        public int page { get; set; }
        public int pagesize { get; set; }
    }

    public class UserService
    {
        public UserService()
        {
            CallCount = 0;
        }

        public int CallCount { get; private set; }
        public User GetUser()
        {
            CallCount += 1;
            return new User();
        }
        public IEnumerable<User> GetUsers(int? id = null)
        {
            CallCount += 1;
            return new List<User> { new User
                {
                    Id = id ?? 0,
                }
            };
        }

        internal User GetUserById(int id)
        {
            CallCount += 1;
            return new User
            {
                Id = id,
            };
        }
    }
    public class AgeService
    {
        public AgeService()
        {
            CallCount = 0;
        }

        public int CallCount { get; private set; }

        public async Task<int> GetAgeAsync(DateTime? birthday)
        {
            return await System.Threading.Tasks.Task.Run(() => birthday.HasValue ? (int)(DateTime.Now - birthday.Value).TotalDays / 365 : 0);
        }

        public int GetAge(DateTime? birthday)
        {
            CallCount += 1;
            // you could do smarter things here like use other services
            return birthday.HasValue ? (int)(DateTime.Now - birthday.Value).TotalDays / 365 : 0;
        }
    }

    public class ProjectConfig
    {
        public string Type { get; set; }
    }
}