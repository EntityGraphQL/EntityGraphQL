using Xunit;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Linq;
using static EntityGraphQL.Schema.ArgumentHelper;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace EntityGraphQL.Tests
{
    public class ComplexFieldsTests
    {
        [Fact]
        public void TestServicesAtRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                (db, p) => WithService((EntityPager pager) => pager.PageProjects(db, p)),
                "Pagination. [defaults: page = 1, pagesize = 10]");

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

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
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
                (person, p) => WithService((EntityPager pager) => pager.PageProjects(person.Projects, p)),
                "Pagination. [defaults: page = 1, pagesize = 10]");

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
            EntityPager pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            dynamic person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Single(person.GetType().GetFields());
            Assert.Equal("projects", person.GetType().GetFields()[0].Name);
        }

        [Fact]
        public void TestServicesNonRootWithOtherFields()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                (person, p) => WithService((EntityPager pager) => pager.PageProjects(person.Projects, p)),
                "Pagination. [defaults: page = 1, pagesize = 10]");

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
            EntityPager pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("projects", person.GetType().GetFields()[0].Name);
            Assert.Equal("name", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void TestServicesNonRootWithOtherFieldsAndRelation()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().ReplaceField("projects",
                new { page = 1, pagesize = 10 },
                (person, p) => WithService((EntityPager pager) => pager.PageProjects(person.Projects, p)),
                "Pagination. [defaults: page = 1, pagesize = 10]");

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
            EntityPager pager = new EntityPager();
            serviceCollection.AddSingleton(pager);

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, pager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("projects", person.GetType().GetFields()[0].Name);
            Assert.Equal("manager", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void TestServicesNonRootWithOtherFieldsAndRelation2()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.AddType<Pagination<Project>>("ProjectPagination").AddAllFields();

            schema.Type<Person>().AddField("age",
                // use a filed not another relation/entity
                (person) => WithService((AgeService ager) => ager.GetAge(person.Birthday)),
                "Persons age");

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

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, ager.CallCount);
            var person = Enumerable.ElementAt((dynamic)res.Data["people"], 0);
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Equal("age", person.GetType().GetFields()[0].Name);
            Assert.Equal("manager", person.GetType().GetFields()[1].Name);
        }

        [Fact]
        public void TestServicesReconnectToSchemaContext()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            // Linking a type from a service back to the schema context
            schema.AddType<User>("User").AddAllFields();
            schema.Type<User>().AddField("projects",
                (user) => WithService((TestDataContext db) => db.Projects.Where(p => p.Owner.Id == user.Id)),
                "Peoples projects");

            schema.AddField("user", ctx => WithService((UserService users) => users.GetUser()), "Get current user");

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
            UserService userService = new UserService();
            serviceCollection.AddSingleton(userService);
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
            Assert.Equal(1, userService.CallCount);
            Assert.Equal(2, res.Data["user"].GetType().GetFields().Count());
            Assert.Equal("id", res.Data["user"].GetType().GetFields().ElementAt(0).Name);
            Assert.Equal("projects", res.Data["user"].GetType().GetFields().ElementAt(1).Name);
        }

        public class AgeService
        {
            public AgeService()
            {
                CallCount = 0;
            }

            public int CallCount { get; private set; }

            public int GetAge(DateTime? birthday)
            {
                CallCount += 1;
                // you could do smarter things here like use other services
                return birthday.HasValue ? (int)(DateTime.Now - birthday.Value).TotalDays / 365 : 0;
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
    }
}