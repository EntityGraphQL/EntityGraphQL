using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class AsyncAdvancedTests
{
    [Fact]
    public void DeeplyNested_Async_Service_Field_Is_Awaited_In_Dynamic_Projection()
    {
        // Schema with async service field on Person
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("ageAsync", "Async age").ResolveAsync<AgeService>((p, srv) => srv.GetAgeAsync(p.Birthday));

        // Also add another async service field to exercise multiple Task<T> in the same anonymous/dynamic object
        schema.Type<Person>().AddField("nicknameAsync", "Async nickname").ResolveAsync<NickService>((p, srv) => srv.GetNicknameAsync(p.Name));

        // Build nested data: Project -> Task -> Assignee(Person)
        var context = new TestDataContext
        {
            Projects = new List<Project>
            {
                new()
                {
                    Id = 1,
                    Tasks = new List<Task>
                    {
                        new()
                        {
                            Assignee = new Person { Name = "Alyssa", Birthday = DateTime.Now.AddYears(-25) },
                        },
                    },
                },
            },
        };

        var services = new ServiceCollection();
        services.AddSingleton(new AgeService());
        services.AddSingleton(new NickService());
        var sp = services.BuildServiceProvider();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    projects {
                        tasks {
                            assignee {
                                ageAsync
                                nicknameAsync
                            }
                        }
                    }
                }",
        };

        var res = schema.ExecuteRequestWithContext(gql, context, sp, null);
        Assert.Null(res.Errors);

        // Validate the async results are resolved (no Task left) and values make sense
        dynamic projects = res.Data!["projects"]!;
        dynamic firstTask = projects[0].tasks[0];
        dynamic assignee = firstTask.assignee;

        // Types should be value types (int/string), not Task
        Assert.IsType<int>(assignee.ageAsync);
        Assert.IsType<string>(assignee.nicknameAsync);
        Assert.Equal("Alyssa_nick", (string)assignee.nicknameAsync);
        Assert.Equal(25, (int)assignee.ageAsync);
    }

    [Fact]
    public void Large_List_Async_Scalar_Fields_Are_Resolved_Once_Each()
    {
        const int N = 200;
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        // Counter async service to track calls
        var counter = new CounterService();
        schema.Type<Person>().AddField("counter", "Async counter test").ResolveAsync<CounterService>((p, srv) => srv.GetValueAsync(p.Id));

        // Populate data
        var ctx = new TestDataContext { People = Enumerable.Range(1, N).Select(i => new Person { Id = i }).ToList() };

        var services = new ServiceCollection();
        services.AddSingleton(counter);
        var sp = services.BuildServiceProvider();

        var gql = new QueryRequest { Query = @"query { people { counter } }" };

        var res = schema.ExecuteRequestWithContext(gql, ctx, sp, null);
        Assert.Null(res.Errors);

        dynamic people = res.Data!["people"]!;
        Assert.Equal(N, ((IEnumerable<dynamic>)people).Count());
        Assert.IsType<int>(people[0].counter);
        // Ensure each item resolved exactly once (no double invocation)
        Assert.Equal(N, counter.CallCount);
    }
}

internal class NickService
{
    public System.Threading.Tasks.Task<string> GetNicknameAsync(string? name)
    {
        return System.Threading.Tasks.Task.FromResult((name ?? "").Trim() + "_nick");
    }
}

internal class CounterService
{
    public int CallCount { get; private set; }

    public async System.Threading.Tasks.Task<int> GetValueAsync(int id)
    {
        CallCount += 1;
        // Simulate async boundary
        await System.Threading.Tasks.Task.Yield();
        return id;
    }
}
