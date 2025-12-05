using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class FilteredFieldTests
{
    [Fact]
    public void TestUseConstFilter()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("projects", new { search = (string?)null }, (ctx, args) => ctx.Projects.OrderBy(p => p.Id), "List of projects");

        Func<Task, bool> TaskFilter = t => t.IsActive;
        schema.Type<Project>().ReplaceField("tasks", p => p.Tasks.Where(TaskFilter), "Active tasks");

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    projects {
                        tasks { id }
                    }
                }",
        };

        var context = new TestDataContext { Projects = [new Project { Tasks = new List<Task> { new Task() } }] };

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
        dynamic project = Enumerable.ElementAt((dynamic)res.Data!["projects"]!, 0);
        Type projectType = project.GetType();
        Assert.Single(projectType.GetFields());
        Assert.Equal("tasks", projectType.GetFields()[0].Name);
    }

    [Fact]
    public void TestWhereWhenOnNonRootField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema
            .Type<Project>()
            .ReplaceField(
                "tasks",
                new { like = (string?)null },
                (project, args) => project.Tasks.WhereWhen(t => t.Name.Contains(args.like!), !string.IsNullOrEmpty(args.like)),
                "List of project tasks"
            );

        var gql = new QueryRequest
        {
            Query =
                @"{
                    projects {
                        tasks(like: ""h"") { name }
                    }
                }",
        };

        var context = new TestDataContext
        {
            Projects =
            [
                new Project
                {
                    Tasks = new List<Task>
                    {
                        new Task { Name = "hello" },
                        new Task { Name = "world" },
                    },
                    Description = "Hello",
                },
            ],
        };

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
        dynamic project = Enumerable.First((dynamic)res.Data!["projects"]!);
        Type projectType = project.GetType();
        Assert.Single(projectType.GetFields());
        Assert.Equal("tasks", projectType.GetFields()[0].Name);
    }

    [Fact]
    public void TestOffsetPagingWithOthersAndServices()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        data.People.Add(
            new Person
            {
                Id = 1,
                Name = "Jill",
                LastName = "Frank",
                Birthday = DateTime.Now.AddYears(-22),
            }
        );
        data.People.Add(
            new Person
            {
                Id = 2,
                Name = "Cheryl",
                LastName = "Frank",
                Birthday = DateTime.Now.AddYears(-10),
            }
        );

        schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people").UseFilter();
        schema.Type<Person>().AddField("age", "Persons age").Resolve<AgeService>((person, ager) => ager.GetAge(person.Birthday));
        var gql = new QueryRequest
        {
            Query =
                @"{
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

        dynamic people = result.Data!["people"]!;
        Assert.Equal(1, Enumerable.Count(people));
        var person1 = Enumerable.ElementAt(people, 0);
        Assert.Equal("Frank", person1.lastName);
        Assert.Equal("Jill", person1.name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TestFilterWithServiceReference(bool separateServices)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        data.People.Add(
            new Person
            {
                Id = 1,
                Name = "Jill",
                LastName = "Frank",
                Birthday = DateTime.Now.AddYears(-22),
            }
        );
        data.People.Add(
            new Person
            {
                Id = 2,
                Name = "Cheryl",
                LastName = "Frank",
                Birthday = DateTime.Now.AddYears(-10),
            }
        );

        schema.Query().ReplaceField("people", ctx => ctx.People, "Return list of people").UseFilter();
        schema.Type<Person>().AddField("age", "Persons age").Resolve<AgeService>((person, ager) => ager.GetAge(person.Birthday));
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people(filter: ""age > 21"") {
                        name id age lastName
                    }
                }",
        };

        var serviceCollection = new ServiceCollection();
        var ager = new AgeService();
        serviceCollection.AddSingleton(ager);

        var result = schema.ExecuteRequestWithContext(gql, data, serviceCollection.BuildServiceProvider(), null, new ExecutionOptions { ExecuteServiceFieldsSeparately = separateServices });

        // Both scenarios should now work - filtering with service fields is supported
        Assert.Null(result.Errors);
        dynamic people = result.Data!["people"]!;
        Assert.Equal(1, Enumerable.Count(people));
        var person1 = Enumerable.ElementAt(people, 0);
        Assert.Equal("Frank", person1.lastName);
        Assert.Equal("Jill", person1.name);
    }

    [Fact]
    public void TestUseFilterOnFieldWithExistingArgumentsAddsFilterArgumentWithoutDefault()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        // Add a field with existing arguments then apply UseFilter
        schema
            .Type<TestDataContext>()
            .ReplaceField("projects", new { search = (string?)null, limit = 10 }, (ctx, args) => ctx.Projects.Take(args.limit), "List of projects with search and limit")
            .UseFilter();

        // Get the GraphQL schema as SDL
        var sdl = schema.ToGraphQLSchemaString();

        // Look for the projects field definition and check arguments
        Assert.Contains("projects(", sdl);
        Assert.Contains("search: String", sdl);
        Assert.Contains("limit: Int!", sdl);
        Assert.Contains("filter: String", sdl);

        // Ensure filter argument doesn't have default value
        Assert.DoesNotContain("filter: String = ", sdl);
    }

    [Fact(Skip = "This test currently fails - see GitHub issue #378")]
    public void ReproducesGitHubIssue378_CountMethodNotFoundOnOffsetPage()
    {
        var schema = SchemaBuilder.FromObject<IncidentContext>();

        // Set up the exact hierarchical scenario from GitHub issue #378:
        // incident -> enquiries (with filtering and paging) -> enquirerDaps (with filtering and paging)
        schema.Type<Incident>().GetField("enquiries", null).UseFilter().UseOffsetPaging();
        schema.Type<Enquiry>().GetField("enquirerDaps", null).UseFilter().UseOffsetPaging();

        var gql = new QueryRequest
        {
            Query =
                @"
                query {
                    incident(id: 1) {
                        id
                        enquiries(filter: ""enquirerDaps.selectMany(items).count() > 0"", skip: 0, take: null) {
                            items {
                                enquirerDaps(filter: ""dapId==209"") {
                                    items { 
                                        dapId
                                    }
                                }
                            }
                        }
                    }
                }",
        };

        var context = new IncidentContext().FillWithIncidentData();
        var result = schema.ExecuteRequestWithContext(gql, context, null, null);

        // This test reproduces GitHub issue #378:
        // https://github.com/EntityGraphQL/EntityGraphQL/issues/378
        Assert.Null(result.Errors);
        dynamic incident = ((IDictionary<string, object>)result.Data!)["incident"];
        Assert.Equal(3, incident.enquiries.items);
        var enquiry = Enumerable.First(incident.enquiries.items);
        Assert.Equal(1, enquiry.id);
        Assert.Single(enquiry.enquirerDaps.items);
        var enquirerDap = Enumerable.First(enquiry.enquirerDaps.items);
        Assert.Equal(209, enquirerDap.dapId);
    }

    private class IncidentContext
    {
        public List<Incident> Incidents { get; set; } = [];

        public IncidentContext FillWithIncidentData()
        {
            var incident = new Incident { Id = 1 };

            // Enquiry with matching enquirerDaps (should be returned)
            var enquiryWithDaps = new Enquiry { Id = 1, EnquirerDaps = [new EnquirerDap { DapId = 209 }, new EnquirerDap { DapId = 210 }] };

            // Enquiry with no enquirerDaps (should be filtered out)
            var enquiryWithoutDaps = new Enquiry { Id = 2, EnquirerDaps = [] };

            // Enquiry with non-matching enquirerDaps (should be filtered out)
            var enquiryWithOtherDaps = new Enquiry { Id = 3, EnquirerDaps = [new EnquirerDap { DapId = 999 }] };

            incident.Enquiries = [enquiryWithDaps, enquiryWithoutDaps, enquiryWithOtherDaps];
            Incidents.Add(incident);

            return this;
        }
    }

    private class Incident
    {
        public int Id { get; set; }
        public List<Enquiry> Enquiries { get; set; } = [];
    }

    private class Enquiry
    {
        public int Id { get; set; }
        public List<EnquirerDap> EnquirerDaps { get; set; } = [];
    }

    private class EnquirerDap
    {
        public int DapId { get; set; }
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
    }

    private class Job
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
