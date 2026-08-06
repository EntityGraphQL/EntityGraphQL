using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// A paging field whose resolver uses a service inside its own expression - e.g.
/// <c>p.Tasks.Where(t => t.HoursEstimated > limits.MinHours)</c> - cannot be split across the two passes, so
/// the engine builds it in one go ("interleaved"). That works for a root field (see
/// <c>ConnectionPagingTests.TestResolveWithServiceAndConnectionPaging</c>, issue #459); these cover the same
/// thing on a field of a nested type, where the first pass has to leave a complete value for the second to
/// read rather than an extracted dependency it can no longer resolve.
/// </summary>
public class NestedInterleavedServicePagingTests
{
    private class HourLimits
    {
        public float MinHours => 0;
    }

    private class TaskLabels
    {
        public string GetLabel(int id) => $"label-{id}";
    }

    private static (SchemaProvider<TestDataContext> schema, TestDataContext data, ServiceProvider sp) Build()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Id = 1,
                    Name = "Project 1",
                    Tasks = [new Task { Id = 1, Name = "Task 1", HoursEstimated = 5 }, new Task { Id = 2, Name = "Task 2", HoursEstimated = 8 }],
                },
            ],
        };
        var services = new ServiceCollection();
        services.AddSingleton(new HourLimits());
        services.AddSingleton(new TaskLabels());
        return (schema, data, services.BuildServiceProvider());
    }

    [Fact]
    public void OffsetPagingWithServiceInResolver_OnNestedType()
    {
        var (schema, data, sp) = Build();
        schema
            .Type<Project>()
            .ReplaceField("tasks", "Get a page of tasks")
            .Resolve<HourLimits>((p, limits) => p.Tasks.Where(t => t.HoursEstimated >= limits.MinHours).OrderBy(t => t.Id))
            .UseOffsetPaging();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ projects { tasks(take: 2) { items { id name } totalItems } } }" }, data, sp, null);

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects[0].tasks.totalItems);
        Assert.Equal("Task 1", projects[0].tasks.items[0].name);
    }

    [Fact]
    public void ConnectionPagingWithServiceInResolver_OnNestedType()
    {
        var (schema, data, sp) = Build();
        schema
            .Type<Project>()
            .ReplaceField("tasks", "Get a page of tasks")
            .Resolve<HourLimits>((p, limits) => p.Tasks.Where(t => t.HoursEstimated >= limits.MinHours).OrderBy(t => t.Id))
            .UseConnectionPaging();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ projects { tasks(first: 2) { edges { node { id name } } totalCount } } }" }, data, sp, null);

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects[0].tasks.totalCount);
        Assert.Equal("Task 1", projects[0].tasks.edges[0].node.name);
    }

    /// <summary>
    /// As above but the paged items select another service field. The interleaved page is built in the first
    /// pass, so that field's dependency has to be resolvable against what the first pass left behind.
    /// </summary>
    [Fact]
    public void OffsetPagingWithServiceInResolver_OnNestedType_ItemsSelectAServiceField()
    {
        var (schema, data, sp) = Build();
        schema
            .Type<Project>()
            .ReplaceField("tasks", "Get a page of tasks")
            .Resolve<HourLimits>((p, limits) => p.Tasks.Where(t => t.HoursEstimated >= limits.MinHours).OrderBy(t => t.Id))
            .UseOffsetPaging();
        schema.Type<Task>().AddField("label", "Label from a service").Resolve<TaskLabels>((t, labels) => labels.GetLabel(t.Id));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ projects { tasks(take: 2) { items { id label } totalItems } } }" }, data, sp, null);

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal("label-1", projects[0].tasks.items[0].label);
    }

    /// <summary>Connection paging equivalent of the above.</summary>
    [Fact]
    public void ConnectionPagingWithServiceInResolver_OnNestedType_NodesSelectAServiceField()
    {
        var (schema, data, sp) = Build();
        schema
            .Type<Project>()
            .ReplaceField("tasks", "Get a page of tasks")
            .Resolve<HourLimits>((p, limits) => p.Tasks.Where(t => t.HoursEstimated >= limits.MinHours).OrderBy(t => t.Id))
            .UseConnectionPaging();
        schema.Type<Task>().AddField("label", "Label from a service").Resolve<TaskLabels>((t, labels) => labels.GetLabel(t.Id));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ projects { tasks(first: 2) { edges { node { id label } } totalCount } } }" }, data, sp, null);

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal("label-1", projects[0].tasks.edges[0].node.label);
    }

    /// <summary>Services in one pass - the documented way out for interleaved paging - must keep working.</summary>
    [Fact]
    public void OffsetPagingWithServiceInResolver_OnNestedType_SinglePass()
    {
        var (schema, data, sp) = Build();
        schema
            .Type<Project>()
            .ReplaceField("tasks", "Get a page of tasks")
            .Resolve<HourLimits>((p, limits) => p.Tasks.Where(t => t.HoursEstimated >= limits.MinHours).OrderBy(t => t.Id))
            .UseOffsetPaging();

        var res = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ projects { tasks(take: 2) { items { id name } totalItems } } }" },
            data,
            sp,
            null,
            new ExecutionOptions { ExecuteServiceFieldsSeparately = false }
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic projects = res.Data!["projects"]!;
        Assert.Equal(2, projects[0].tasks.totalItems);
    }
}
