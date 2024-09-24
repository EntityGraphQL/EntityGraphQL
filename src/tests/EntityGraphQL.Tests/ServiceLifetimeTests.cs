using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class ServiceLifetimeTests
{
    [Fact]
    public void TestDataContextLifetimeTransientTwoTimes()
    {
        var schema = SchemaBuilder.FromObject<MyDataContext>();
        var services = new ServiceCollection();
        var allContexts = new List<MyDataContext>();
        services.AddTransient(sp =>
        {
            var context = new MyDataContext();
            allContexts.Add(context);
            return context;
        });
        var provider = services.BuildServiceProvider();

        var gql = new QueryRequest
        {
            Query =
                @"{
                name
                movies {
                    title
                }
            }"
        };

        var result = schema.ExecuteRequest(gql, provider, null, null);
        Assert.Null(result.Errors);
        Assert.Equal(2, allContexts.Count);
        Assert.NotSame(allContexts[0], allContexts[1]);
    }

    [Fact]
    public void TestDataContextLifetimeTransientField()
    {
        var schema = SchemaBuilder.FromObject<MyDataContext>();
        schema.UpdateType<MyMovie>(movie =>
            movie
                .AddField("directorAgeOnRelease", "Age of the director on release date of the movie")
                .Resolve<MyDataContext>((movie, context) => (int)(context.Directors.First(d => d.Id == movie.DirectorId).Dob - movie.Released).TotalDays / 365)
        );
        var services = new ServiceCollection();
        var allContexts = new List<MyDataContext>();
        services.AddTransient(sp =>
        {
            var context = new MyDataContext();
            allContexts.Add(context);
            return context;
        });
        var provider = services.BuildServiceProvider();

        var gql = new QueryRequest
        {
            Query =
                @"{
                name
                movies {
                    title
                    directorAgeOnRelease
                }
            }"
        };

        var result = schema.ExecuteRequest(gql, provider, null, null);
        Assert.Null(result.Errors);
        Assert.Equal(3, allContexts.Count);
        Assert.NotSame(allContexts[0], allContexts[1]);
        Assert.NotSame(allContexts[0], allContexts[2]);
        Assert.NotSame(allContexts[1], allContexts[2]);
    }

    [Fact]
    public void TestDataContextLifetimeTransientFieldManySameService()
    {
        var schema = SchemaBuilder.FromObject<MyDataContext>();
        schema.UpdateType<MyMovie>(movie =>
        {
            movie
                .AddField("directorAgeOnRelease", "Age of the director on release date of the movie")
                .Resolve<MyDataContext>((movie, context) => (int)(context.Directors.First(d => d.Id == movie.DirectorId).Dob - movie.Released).TotalDays / 365);

            movie
                .AddField("hoursOld", "Reusing the same Transient service should be a new service instance")
                .Resolve<MyDataContext>((movie, context) => (int)(context.Directors.First(d => d.Id == movie.DirectorId).Dob - movie.Released).TotalHours);
        });
        var services = new ServiceCollection();
        var allContexts = new List<MyDataContext>();
        services.AddTransient(sp =>
        {
            var context = new MyDataContext();
            allContexts.Add(context);
            return context;
        });
        var provider = services.BuildServiceProvider();

        var gql = new QueryRequest
        {
            Query =
                @"{
                name
                movies {
                    title
                    directorAgeOnRelease
                    hoursOld
                }
            }"
        };

        var result = schema.ExecuteRequest(gql, provider, null, null);
        Assert.Null(result.Errors);
        Assert.Equal(4, allContexts.Count);
        Assert.NotSame(allContexts[0], allContexts[1]);
        Assert.NotSame(allContexts[0], allContexts[2]);
        Assert.NotSame(allContexts[0], allContexts[3]);
        Assert.NotSame(allContexts[1], allContexts[2]);
        Assert.NotSame(allContexts[1], allContexts[3]);
        Assert.NotSame(allContexts[3], allContexts[2]);
    }

    [Fact]
    public void TestDataContextLifetimeScoped()
    {
        var schema = SchemaBuilder.FromObject<MyDataContext>();
        schema.UpdateType<MyMovie>(movie =>
        {
            movie
                .AddField("directorAgeOnRelease", "Age of the director on release date of the movie")
                .Resolve<MyDataContext>((movie, context) => (int)(context.Directors.First(d => d.Id == movie.DirectorId).Dob - movie.Released).TotalDays / 365);

            movie
                .AddField("hoursOld", "Reusing the same Transient service should be a new service instance")
                .Resolve<MyDataContext>((movie, context) => (int)(context.Directors.First(d => d.Id == movie.DirectorId).Dob - movie.Released).TotalHours);
        });
        var services = new ServiceCollection();
        var allContexts = new List<MyDataContext>();
        services.AddScoped(sp =>
        {
            var context = new MyDataContext();
            allContexts.Add(context);
            return context;
        });
        var provider = services.BuildServiceProvider();

        var gql = new QueryRequest
        {
            Query =
                @"{
                name
                movies {
                    title
                    directorAgeOnRelease
                    hoursOld
                }
            }"
        };

        var result = schema.ExecuteRequest(gql, provider, null, null);
        Assert.Null(result.Errors);
        Assert.Single(allContexts);
    }

    [Fact]
    public void TestDataContextLifetimeSingleton()
    {
        var schema = SchemaBuilder.FromObject<MyDataContext>();
        schema.UpdateType<MyMovie>(movie =>
        {
            movie
                .AddField("directorAgeOnRelease", "Age of the director on release date of the movie")
                .Resolve<MyDataContext>((movie, context) => (int)(context.Directors.First(d => d.Id == movie.DirectorId).Dob - movie.Released).TotalDays / 365);

            movie
                .AddField("hoursOld", "Reusing the same Transient service should be a new service instance")
                .Resolve<MyDataContext>((movie, context) => (int)(context.Directors.First(d => d.Id == movie.DirectorId).Dob - movie.Released).TotalHours);
        });
        var services = new ServiceCollection();
        var allContexts = new List<MyDataContext>();
        services.AddSingleton(sp =>
        {
            var context = new MyDataContext();
            allContexts.Add(context);
            return context;
        });
        var provider = services.BuildServiceProvider();

        var gql = new QueryRequest
        {
            Query =
                @"{
                name
                movies {
                    title
                    directorAgeOnRelease
                    hoursOld
                }
            }"
        };

        var result = schema.ExecuteRequest(gql, provider, null, null);
        Assert.Null(result.Errors);
        Assert.Single(allContexts);
    }
}

internal class MyDataContext
{
    public string Name { get; set; } = "Test";
    public List<MyMovie> Movies { get; set; } =
        new List<MyMovie>
        {
            new MyMovie
            {
                Id = 1,
                Title = "Movie 1",
                DirectorId = 11,
                Released = new DateTime(1999, 6, 19)
            }
        };
    public List<MyDirector> Directors { get; set; } =
        new List<MyDirector>
        {
            new MyDirector
            {
                Id = 11,
                Name = "Director 1",
                Dob = new DateTime(1978, 4, 6)
            }
        };
}

internal class MyDirector
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Dob { get; set; }
}

internal class MyMovie
{
    public string Title { get; set; } = string.Empty;
    public int Id { get; set; }

    public DateTime Released { get; set; }

    public int DirectorId { get; set; }
}
