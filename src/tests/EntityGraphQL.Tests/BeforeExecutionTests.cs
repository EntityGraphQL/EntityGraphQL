using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class BeforeExecutionTests
{
    [Fact]
    public void TestBeforeExecution()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var query = @"{
                    project(id: 99) {
                        name
                        tasks {
                            name id
                        }
                    }
                }";
        var hash = QueryCache.ComputeHash(query);

        var gql = new QueryRequest
        {
            Query = query,
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { });
        Assert.Null(result.Errors);

        dynamic project = result.Data["project"];
        Assert.Equal(5, Enumerable.Count(project.tasks));

        result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions
        {
            BeforeExecuting = (e, isFinal) => Expression.Constant(null)
        });
        Assert.Null(result.Errors);

        project = result.Data["project"];
        Assert.Null(project);
    }


    private static void FillProjectData(TestDataContext data)
    {
        data.Projects = new List<Project>
            {
                new Project
                {
                    Id = 99,
                    Name ="Project 1",
                    Tasks = new List<Task>
                    {
                        new Task
                        {
                            Id = 0,
                            Name = "Task 1"
                        },
                        new Task
                        {
                            Id = 1,
                            Name = "Task 2"
                        },
                        new Task
                        {
                            Id = 2,
                            Name = "Task 3"
                        },
                        new Task
                        {
                            Id = 3,
                            Name = "Task 4"
                        },
                        new Task
                        {
                            Id = 4,
                            Name = "Task 5"
                        },

                    }
                }
            };
    }

}