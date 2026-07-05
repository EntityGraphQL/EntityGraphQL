using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// UseFilter/UseSort on a field whose own resolver is service-backed. The collection does not exist in the
/// first (non-service) execution pass so the extensions must apply their work in the services pass.
/// </summary>
public class ServiceBackedCollectionExtensionsTests
{
    public class PeopleService
    {
        public List<Person> GetPeople() =>
            [
                new Person { Id = 1, Name = "Alyssa", Height = 180 },
                new Person { Id = 2, Name = "Ben", Height = 160 },
                new Person { Id = 3, Name = "Cy", Height = 190 },
            ];
    }

    private static ServiceProvider BuildServices()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new PeopleService());
        return serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void Filter_OnRootServiceBackedCollection_IsApplied()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("servicePeople", "People from a service").Resolve<PeopleService>((ctx, srv) => srv.GetPeople()).UseFilter();

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = @"{ servicePeople(filter: ""height > 170"") { name } }" },
            new TestDataContext(),
            BuildServices(),
            null
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["servicePeople"]!;
        Assert.Equal(2, Enumerable.Count(people));
        Assert.Equal("Alyssa", people[0].name);
        Assert.Equal("Cy", people[1].name);
    }

    [Fact]
    public void Sort_OnRootServiceBackedCollection_IsApplied()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("servicePeople", "People from a service").Resolve<PeopleService>((ctx, srv) => srv.GetPeople()).UseSort();

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = @"{ servicePeople(sort: [{ height: ASC }]) { name height } }" },
            new TestDataContext(),
            BuildServices(),
            null
        );

        Assert.Null(result.Errors);
        dynamic people = result.Data!["servicePeople"]!;
        Assert.Equal(3, Enumerable.Count(people));
        Assert.Equal("Ben", people[0].name);
        Assert.Equal("Alyssa", people[1].name);
        Assert.Equal("Cy", people[2].name);
    }

    [Fact]
    public void Filter_OnNestedServiceBackedCollection_IsApplied()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Project>().AddField("servicePeople", "People from a service").Resolve<PeopleService>((proj, srv) => srv.GetPeople()).UseFilter();

        var data = new TestDataContext { Projects = [new Project { Id = 1, Name = "Project 1" }] };
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = @"{ projects { name servicePeople(filter: ""height > 170"") { name } } }" },
            data,
            BuildServices(),
            null
        );

        Assert.Null(result.Errors);
        dynamic project = Enumerable.First((dynamic)result.Data!["projects"]!);
        Assert.Equal(2, Enumerable.Count(project.servicePeople));
        Assert.Equal("Alyssa", project.servicePeople[0].name);
        Assert.Equal("Cy", project.servicePeople[1].name);
    }

    [Fact]
    public void Sort_OnNestedServiceBackedCollection_IsApplied()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Project>().AddField("servicePeople", "People from a service").Resolve<PeopleService>((proj, srv) => srv.GetPeople()).UseSort();

        var data = new TestDataContext { Projects = [new Project { Id = 1, Name = "Project 1" }] };
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = @"{ projects { name servicePeople(sort: [{ height: ASC }]) { name height } } }" },
            data,
            BuildServices(),
            null
        );

        Assert.Null(result.Errors);
        dynamic project = Enumerable.First((dynamic)result.Data!["projects"]!);
        Assert.Equal(3, Enumerable.Count(project.servicePeople));
        Assert.Equal("Ben", project.servicePeople[0].name);
        Assert.Equal("Alyssa", project.servicePeople[1].name);
        Assert.Equal("Cy", project.servicePeople[2].name);
    }
}
