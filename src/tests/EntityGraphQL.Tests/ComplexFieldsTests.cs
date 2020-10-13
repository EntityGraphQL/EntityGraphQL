using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Xunit;
using EntityGraphQL.Schema;
using System.Linq;
using static EntityGraphQL.Schema.ArgumentHelper;
using Microsoft.Extensions.DependencyInjection;

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
            serviceCollection.AddSingleton(new EntityPager());

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
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
            serviceCollection.AddSingleton(new EntityPager());

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
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
                Query = @"{ user { projects { id } } }"
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
            serviceCollection.AddSingleton(new UserService());
            serviceCollection.AddSingleton(context);

            var res = schema.ExecuteQuery(gql, context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(res.Errors);
        }

        public class EntityPager
        {
            public Pagination<Project> PageProjects(TestDataContext db, dynamic arg)
            {
                Console.WriteLine("called");
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
                System.Console.WriteLine("called");
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
            [Required]
            public IEnumerable<TEntity> Items { get; set; }
            [Required]
            public int Total { get; set; }
            [Required]

            public int PageCount { get; set; }
        }
    }

    public class UserService
    {
        public User GetUser()
        {
            return new User();
        }
    }
}