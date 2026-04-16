using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class AsyncTests
{
    [Fact]
    public void TestAsyncServiceField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("age", "Returns persons age").ResolveAsync<AgeService>((ctx, srv) => srv.GetAgeAsync(ctx.Birthday));

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    people {
                        age
                    }
                }",
        };

        var context = new TestDataContext();
        context.People.Clear();
        context.People.Add(new Person { Birthday = DateTime.Now.AddYears(-2) });

        var serviceCollection = new ServiceCollection();
        AgeService service = new();
        serviceCollection.AddSingleton(service);

        var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);

        Assert.NotNull(res.Data);
        var age = ((dynamic)res.Data!["people"]!)[0].age;
        Assert.Equal(2, age);
    }

    [Fact]
    public void TestAsyncServiceFieldNowSupported()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // Task<> returns are now supported with automatic async resolution
        var field = schema.Type<Person>().AddField("age", "Returns persons age").ResolveAsync<AgeService>((ctx, srv) => srv.GetAgeAsync(ctx.Birthday));
        Assert.NotNull(field);
        Assert.Equal("age", field.Name);
        Assert.Equal(typeof(int), field.ReturnType.TypeDotnet);
    }

    [Fact]
    public void TestReturnsTaskButNotAsync()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add(TestAddPersonAsync);
        Assert.Equal("Person", schema.Mutation().SchemaType.GetField("testAddPersonAsync", null).ReturnType.SchemaType.Name);
        Assert.Equal(typeof(Person), schema.Mutation().SchemaType.GetField("testAddPersonAsync", null).ReturnType.TypeDotnet);
    }

    [Fact]
    public void TestFieldRequiresGenericTask()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // Task<> returns are now supported with automatic async resolution
        Assert.Throws<EntityGraphQLSchemaException>(() =>
        {
            schema.Type<Person>().AddField("age", "Returns persons age").ResolveAsync<AgeService>((ctx, srv) => srv.GetAgeAsyncNoResult(ctx.Birthday));
        });
    }

    private System.Threading.Tasks.Task<Person> TestAddPersonAsync()
    {
        return System.Threading.Tasks.Task.FromResult(new Person());
    }

    [Fact]
    public async System.Threading.Tasks.Task TestCancellationTokenSupport()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        // Add a field that accepts CancellationToken - use the regular async overload for now
        schema
            .Type<Person>()
            .AddField("delayedAge", "Returns age after delay with cancellation support")
            .ResolveAsync<CancellationTestService, CancellationToken>((ctx, srv, ct) => srv.GetAgeWithDelayAsync(ctx.Birthday, ct));

        var gql = new QueryRequest
        {
            Query =
                @"query {
                people {
                    delayedAge
                }
            }",
        };

        var context = new TestDataContext();
        context.People.Clear();
        context.People.Add(new Person { Birthday = DateTime.Now.AddYears(-25) });

        var serviceCollection = new ServiceCollection();
        var service = new CancellationTestService();
        serviceCollection.AddSingleton(service);
        serviceCollection.AddSingleton(context);

        // Test 1: Normal execution (should work)
        var result1 = await schema.ExecuteRequestAsync(gql, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(result1.Errors);
        Assert.NotNull(result1.Data);
        var age1 = ((dynamic)result1.Data!["people"]!)[0].delayedAge;
        Assert.Equal(25, age1);

        // Test 2: With cancelled token should work for now since we're using sync method
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // For now, just test that the feature compiles and works
        var result2 = await schema.ExecuteRequestAsync(gql, serviceCollection.BuildServiceProvider(), null, null, cts.Token);
        Assert.NotNull(result2.Errors);
        Assert.Single(result2.Errors);
        Assert.Equal("The operation was canceled.", result2.Errors[0].Message);
        Assert.Null(result2.Data);
    }

    [Fact]
    public void TestAsyncGraphQLFieldReturnsCorrectSchemaType_Issue488()
    {
        // Test for https://github.com/EntityGraphQL/EntityGraphQL/issues/488
        var schema = SchemaBuilder.FromObject<JobContext>();
        var sdl = schema.ToGraphQLSchemaString();

        // Verify it's defined as an array type (with square brackets)
        // The async Task wrapper should be properly unwrapped to recognize the IEnumerable
        Assert.Contains("jobs(search: String!): [Job!]!", sdl);

        // make sure it executes
        var gql = new QueryRequest
        {
            Query =
                @"{
                    jobs(search: ""Dev"") {
                        id
                        name
                    }
                }",
        };
        var context = new JobContext();
        context.AllJobs.Add(new Job { Id = 1, Name = "DevOps Engineer" });
        context.AllJobs.Add(new Job { Id = 2, Name = "Marketing Manager" });
        var result = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);
        var jobs = (IEnumerable<dynamic>)result.Data!["jobs"]!;
        Assert.Single(jobs); // one job added so should have one result
    }

    [Fact]
    public void TestAsyncGraphQLFieldReturnsCorrectSchemaType_IQueryable_Issue488()
    {
        // Test for https://github.com/EntityGraphQL/EntityGraphQL/issues/488
        // Test async method returning IQueryable<T> - verify schema type is correct
        var schema = SchemaBuilder.FromObject<JobContext>();
        var sdl = schema.ToGraphQLSchemaString();

        // Verify it's defined as an array type (the async Task wrapper should be unwrapped)
        Assert.Contains("jobsQueryable(search: String!): [Job!]!", sdl);

        // make sure it executes
        var gql = new QueryRequest
        {
            Query =
                @"{
                    jobsQueryable(search: ""Dev"") {
                        id
                        name
                    }
                }",
        };
        var context = new JobContext();
        context.AllJobs.Add(new Job { Id = 1, Name = "DevOps Engineer" });
        context.AllJobs.Add(new Job { Id = 2, Name = "Marketing Manager" });
        var result = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);
        var jobs = (IEnumerable<dynamic>)result.Data!["jobsQueryable"]!;
        Assert.Single(jobs); // one job added so should have one result
    }

    [Fact]
    public void TestAsyncGraphQLFieldReturnsCorrectSchemaType_Object_Issue488()
    {
        // Test for https://github.com/EntityGraphQL/EntityGraphQL/issues/488
        // Test async method returning an object
        var schema = SchemaBuilder.FromObject<JobContext>();
        var sdl = schema.ToGraphQLSchemaString();

        // Verify it's defined as Job type (not wrapped in array)
        Assert.Contains("jobById(id: Int!): Job", sdl);

        var gql = new QueryRequest
        {
            Query =
                @"{
                    jobById(id: 1) {
                        id
                        name
                    }
                }",
        };
        var context = new JobContext();
        context.AllJobs.Add(new Job { Id = 1, Name = "DevOps Engineer" });
        context.AllJobs.Add(new Job { Id = 2, Name = "Marketing Manager" });
        var result = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);
        dynamic job = result.Data!["jobById"]!;
        Assert.Equal(1, job.id);
        Assert.Equal("DevOps Engineer", job.name);
    }

    [Fact]
    public void TestAsyncGraphQLFieldReturnsCorrectSchemaType_Scalar_Issue488()
    {
        // Test for https://github.com/EntityGraphQL/EntityGraphQL/issues/488
        // Test async method returning a scalar
        var schema = SchemaBuilder.FromObject<JobContext>();
        var sdl = schema.ToGraphQLSchemaString();

        // Verify it's defined as Int type
        Assert.Contains("jobCount: Int!", sdl);

        var gql = new QueryRequest { Query = @"{ jobCount }" };
        var context = new JobContext();
        context.AllJobs.Add(new Job { Id = 1, Name = "DevOps Engineer" });
        context.AllJobs.Add(new Job { Id = 2, Name = "Marketing Manager" });
        var result = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!["jobCount"]);
    }

    /// <summary>
    /// Reproduces the error:
    /// "No generic method 'SelectWithNullCheck' on type 'EntityGraphQL.Extensions.EnumerableExtensions'
    ///  is compatible with the supplied type arguments and arguments."
    ///
    /// Scenario: root query field returns a single object by ID arg, and that object
    /// has a service field added via UpdateType that resolves async to a list.
    /// The sub-selection of fields on that list triggers the SelectWithNullCheck failure.
    /// </summary>
    [Fact]
    public void TestAsyncServiceFieldReturningListOnSingleObjectFromQueryArg()
    {
        var schema = SchemaBuilder.FromObject<WorkflowContext>();
        schema.AddType<WorkflowTool>("WorkflowTool").AddAllFields();

        schema.UpdateType<Workflow>(type =>
        {
            type.AddField("toolCatalog", "Resolved tool catalog from workflow-bound services.")
                .ResolveAsync<IWorkflowToolCatalogService>((workflow, tools) => tools.GetWorkflowToolCatalogAsync(workflow.Id));
        });

        var gql = new QueryRequest
        {
            Query =
                @"query BuilderGetWorkflow($id: String) {
                workflow(id: $id) {
                    id
                    toolCatalog { name }
                }
            }",
            Variables = new QueryVariables { { "id", "wf-1" } },
        };

        var context = new WorkflowContext();
        context.Workflows.Add(new Workflow { Id = "wf-1", Name = "My Workflow" });

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IWorkflowToolCatalogService>(new WorkflowToolCatalogService());

        var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        dynamic workflow = res.Data!["workflow"]!;
        Assert.Equal("wf-1", workflow.id);
        var catalog = (IEnumerable<dynamic>)workflow.toolCatalog;
        Assert.Single(catalog);
    }

    /// <summary>
    /// Same as TestAsyncServiceFieldReturningListOnSingleObjectFromQueryArg but toolCatalog
    /// is on Step (a list element), so the path is workflow -> steps (list) -> toolCatalog (service).
    /// </summary>
    [Fact]
    public void TestAsyncServiceFieldReturningListNestedInsideList()
    {
        var schema = SchemaBuilder.FromObject<WorkflowContext>();
        schema.AddType<WorkflowTool>("WorkflowTool").AddAllFields();

        schema.UpdateType<Step>(type =>
        {
            type.AddField("toolCatalog", "Resolved tool catalog for this step.").ResolveAsync<IWorkflowToolCatalogService>((step, tools) => tools.GetWorkflowToolCatalogAsync(step.WorkflowId));
        });

        var gql = new QueryRequest
        {
            Query =
                @"query BuilderGetWorkflow($id: String) {
                workflow(id: $id) {
                    id
                    steps {
                        id
                        toolCatalog { name }
                    }
                }
            }",
            Variables = new QueryVariables { { "id", "wf-1" } },
        };

        var context = new WorkflowContext();
        context.Workflows.Add(
            new Workflow
            {
                Id = "wf-1",
                Name = "My Workflow",
                Steps = [new Step { Id = "s-1", WorkflowId = "wf-1" }],
            }
        );

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IWorkflowToolCatalogService>(new WorkflowToolCatalogService());

        var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        dynamic workflow = res.Data!["workflow"]!;
        Assert.Equal("wf-1", workflow.id);
        var steps = (IEnumerable<dynamic>)workflow.steps;
        Assert.Single(steps);
        var catalog = (IEnumerable<dynamic>)((dynamic)steps.First()).toolCatalog;
        Assert.Single(catalog);
        Assert.Equal("tool-a", ((dynamic)catalog.First()).name);
    }

    /// <summary>
    /// Same as TestAsyncServiceFieldReturningListNestedInsideList but the service returns
    /// Task{List{T}} (a concrete collection type) rather than Task{IEnumerable{T}}.
    /// Verifies that SelectWithNullCheck can be called regardless of the concrete async return type.
    /// </summary>
    [Fact]
    public void TestAsyncServiceFieldReturningConcreteListNestedInsideList()
    {
        var schema = SchemaBuilder.FromObject<WorkflowContext>();
        schema.AddType<WorkflowTool>("WorkflowTool").AddAllFields();

        schema.UpdateType<Step>(type =>
        {
            type.AddField("toolCatalog", "Resolved tool catalog for this step.")
                .ResolveAsync<IWorkflowToolCatalogServiceConcreteReturn>((step, tools) => tools.GetWorkflowToolCatalogAsync(step.WorkflowId));
        });

        var gql = new QueryRequest
        {
            Query =
                @"query BuilderGetWorkflow($id: String) {
                workflow(id: $id) {
                    id
                    steps {
                        id
                        toolCatalog { name }
                    }
                }
            }",
            Variables = new QueryVariables { { "id", "wf-1" } },
        };

        var context = new WorkflowContext();
        context.Workflows.Add(
            new Workflow
            {
                Id = "wf-1",
                Name = "My Workflow",
                Steps = [new Step { Id = "s-1", WorkflowId = "wf-1" }],
            }
        );

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IWorkflowToolCatalogServiceConcreteReturn>(new WorkflowToolCatalogServiceConcreteReturn());

        var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        dynamic workflow = res.Data!["workflow"]!;
        Assert.Equal("wf-1", workflow.id);
        var steps = (IEnumerable<dynamic>)workflow.steps;
        Assert.Single(steps);
        var catalog = (IEnumerable<dynamic>)((dynamic)steps.First()).toolCatalog;
        Assert.Single(catalog);
        Assert.Equal("tool-a", ((dynamic)catalog.First()).name);
    }

    private class WorkflowContext
    {
        public List<Workflow> Workflows { get; set; } = [];
    }

    private class Workflow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<Step> Steps { get; set; } = [];
    }

    private class Step
    {
        public string Id { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
    }

    private class WorkflowTool
    {
        public string Name { get; set; } = string.Empty;
    }

    private interface IWorkflowToolCatalogService
    {
        System.Threading.Tasks.Task<IEnumerable<WorkflowTool>> GetWorkflowToolCatalogAsync(string workflowId);
    }

    private class WorkflowToolCatalogService : IWorkflowToolCatalogService
    {
        public System.Threading.Tasks.Task<IEnumerable<WorkflowTool>> GetWorkflowToolCatalogAsync(string workflowId)
        {
            IEnumerable<WorkflowTool> tools = [new WorkflowTool { Name = "tool-a" }];
            return System.Threading.Tasks.Task.FromResult(tools);
        }
    }

    private interface IWorkflowToolCatalogServiceConcreteReturn
    {
        System.Threading.Tasks.Task<List<WorkflowTool>> GetWorkflowToolCatalogAsync(string workflowId);
    }

    private class WorkflowToolCatalogServiceConcreteReturn : IWorkflowToolCatalogServiceConcreteReturn
    {
        public System.Threading.Tasks.Task<List<WorkflowTool>> GetWorkflowToolCatalogAsync(string workflowId)
        {
            return System.Threading.Tasks.Task.FromResult(new List<WorkflowTool> { new WorkflowTool { Name = "tool-a" } });
        }
    }

    private class JobContext
    {
        [GraphQLIgnore]
        public List<Job> AllJobs { get; set; } = [];

        [GraphQLField("jobs", "Search for jobs")]
        public async Task<IEnumerable<Job>> JobSearch(string search)
        {
            // Simulate async operation
            await System.Threading.Tasks.Task.Delay(1);
            return AllJobs.Where(j => j.Name.Contains(search));
        }

        [GraphQLField("jobsQueryable", "Search for jobs returning IQueryable")]
        public async Task<IQueryable<Job>> JobSearchQueryable(string search)
        {
            // Simulate async operation
            await System.Threading.Tasks.Task.Delay(1);
            return AllJobs.Where(j => j.Name.Contains(search)).AsQueryable();
        }

        [GraphQLField("jobById", "Get a job by ID")]
        public async Task<Job?> GetJobById(int id)
        {
            // Simulate async operation
            await System.Threading.Tasks.Task.Delay(1);
            return AllJobs.FirstOrDefault(j => j.Id == id);
        }

        [GraphQLField("jobCount", "Get total job count")]
        public async Task<int> GetJobCount()
        {
            // Simulate async operation
            await System.Threading.Tasks.Task.Delay(1);
            return AllJobs.Count;
        }
    }

    private class Job
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
