using Xunit;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Linq;
using System;
using EntityGraphQL.Extensions;

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

            var res = schema.ExecuteRequest(gql, context, null, null);
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

            var res = schema.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);
            dynamic project = Enumerable.First((dynamic)res.Data["projects"]);
            Type projectType = project.GetType();
            Assert.Single(projectType.GetFields());
            Assert.Equal("tasks", projectType.GetFields()[0].Name);
        }
    }
}