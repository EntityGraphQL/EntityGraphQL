using Xunit;
using System.Collections.Generic;
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
            Assert.Empty(res.Errors);
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
            Assert.Empty(res.Errors);
        }

        public class EntityPager
        {
            public Pagination<Project> PageProjects(TestDataContext db, dynamic arg)
            {
                System.Console.WriteLine("called");
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
            [GraphQLNotNull]
            public IEnumerable<TEntity> Items { get; set; }
            [GraphQLNotNull]
            public int Total { get; set; }
            [GraphQLNotNull]

            public int PageCount { get; set; }
        }
    }
}