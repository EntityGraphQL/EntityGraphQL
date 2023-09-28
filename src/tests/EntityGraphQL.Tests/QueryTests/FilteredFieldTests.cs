using Xunit;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Linq;
using System;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Tests
{
    public class FilteredFieldTests
    {
        [Fact]
        public void TestUseConstFilter()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().ReplaceField("projects",
                new
                {
                    search = (string)null
                },
                (ctx, args) => ctx.Projects.OrderBy(p => p.Id),
                "List of projects");

            Func<Task, bool> TaskFilter = t => t.IsActive == true;
            schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.Where(TaskFilter), "Active tasks");

            var gql = new QueryRequest
            {
                Query = @"query {
                    projects {
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
                        Tasks = new List<Task> { new Task() },
                    }
                },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.ElementAt((dynamic)res.Data["projects"], 0);
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("tasks", projectType.GetFields()[0].Name);
        }

        [Fact]
        public void TestWhereWhenOnNonRootField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.Type<Project>().ReplaceField("tasks",
                new
                {
                    like = (string)null
                },
                (project, args) => project.Tasks.WhereWhen(t => t.Name.Contains(args.like), !string.IsNullOrEmpty(args.like)),
                "List of project tasks");

            var gql = new QueryRequest
            {
                Query = @"{
                    projects {
                        tasks(like: ""h"") { name }
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
                            new Task { Name = "hello" },
                            new Task { Name = "world" },
                        },
                        Description = "Hello"
                    }
                },
            };

            var res = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.First((dynamic)res.Data["projects"]);
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("tasks", projectType.GetFields()[0].Name);
        }

        [Fact(Skip = "Not implemented yet. Need to know that the filter uses the service field age")]
        public void TestOffsetPagingWithOthersAndServices()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            data.People.Add(new Person { Id = 1, Name = "Jill", LastName = "Frank", Birthday = DateTime.Now.AddYears(22) });
            data.People.Add(new Person { Id = 2, Name = "Cheryl", LastName = "Frank", Birthday = DateTime.Now.AddYears(10) });

            schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people")
                .UseFilter();
            schema.Type<Person>().AddField("age", "Persons age")
                .ResolveWithService<AgeService>((person, ager) => ager.GetAge(person.Birthday));
            var gql = new QueryRequest
            {
                Query = @"{
                    people(filter: ""age > 21"") {
                        name id age lastName
                    }
                }",
            };

            var serviceCollection = new ServiceCollection();
            var ager = new AgeService();
            serviceCollection.AddSingleton(ager);

            var result = schema.ExecuteRequestWithContext(gql, data, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(1, Enumerable.Count(people));
            var person1 = Enumerable.ElementAt(people, 0);
            Assert.Equal("Frank", person1.lastName);
            Assert.Equal("Jill", person1.name);
        }
    }
}