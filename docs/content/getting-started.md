---
title: "Getting Started"
metaTitle: "Getting started - EntityGraphQL"
metaDescription: "Get up and running with EntityGraphQL"
---

# Installation
Install via [Nuget](https://www.nuget.org/packages/EntityGraphQL.AspNet) (there is the base Install via [EntityGraphQL](https://www.nuget.org/packages/EntityGraphQL) package with no ASP.NET dependency if required)

# Create a data model

_Note: There is no dependency on Entity Framework. Queries are compiled to `IQueryable` or `IEnumberable` LINQ expressions. EF is not a requirement - any ORM working with `LinqProvider` or an in-memory object will work - this example uses EF._

```
public class DemoContext : DbContext
{
    public DbSet<Movie> Movies { get; set; }
    public DbSet<Person> People { get; set; }
    public DbSet<Actor> Actors { get; set; }
}

public class Movie
{
    public uint Id { get; set; }
    public string Name { get; set; }
    public Genre Genre { get; set; }
    public DateTime Released { get; set; }
    public List<Actor> Actors { get; set; }
    public List<Writer> Writers { get; set; }
    public Person Director { get; set; }
    public uint? DirectorId { get; set; }
    public double Rating { get; internal set; }
}

public class Actor
{
    public uint PersonId { get; set; }
    public Person Person { get; set; }
    public uint MovieId { get; set; }
    public Movie Movie { get; set; }
}
public class Writer
{
    public uint PersonId { get; set; }
    public Person Person { get; set; }
    public uint MovieId { get; set; }
    public Movie Movie { get; set; }
}

public enum Genre
{
    Action,
    Drama,
    Comedy,
    Horror,
    Scifi,
}

public class Person
{
    public uint Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime Dob { get; set; }
    public List<Actor> ActorIn { get; set; }
    public List<Writer> WriterOf { get; set; }
    public List<Movie> DirectorOf { get; set; }
    public DateTime? Died { get; set; }
    public bool IsDeleted { get; set; }
}
```

# Create the API

Using what ever .NET API library you wish you can receive a query, execute it and return the data. Here is an example with ASP.NET.

You will need to install [EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) to use `MapGraphQL<>()` and `AddGraphQLSchema()`. You can also build your own endpoint, see below.

[![Nuget](https://img.shields.io/nuget/dt/EntityGraphQL.AspNet)](https://www.nuget.org/packages/EntityGraphQL.AspNet)

```
public class Startup {
  public void ConfigureServices(IServiceCollection services)
  {
      services.AddDbContext<DemoContext>(opt => opt.UseInMemoryDatabase()); // Again this example using EF but you do not have to
      // This registers a SchemaProvider<DemoContext>
      services.AddGraphQLSchema<DemoContext>();
  }

  public void Configure(IApplicationBuilder app, DemoContext db)
  {
      app.UseRouting();
      app.UseEndpoints(endpoints =>
      {
          // default to /graphql endpoint
          endpoints.MapGraphQL<DemoContext>();
      });
  }
}
```

This sets up a `HTTP` `POST` end point at `/graphql` where the body of the post is expected to be a GraphQL query. You can change the path with the `path` argument in `MapGraphQL<T>()`

_You can authorize the route how ever you wish using ASP.NET. See the Authorization section for more details._

You can also expose any endpoint over any protocol you'd like. We'll use HTTP/S for these examples.

# Query your API

You can now make a request to your API via any HTTP tool/library.

For example
```
  POST localhost:5000/graphql
  {
    "query": "{
      movies { id name }
    }",
    "variables": null
  }
```

Will return the following result (depending on the data in you DB).

```
{
  "data": {
    "movies": [
      {
        "id": 11,
        "name": "Inception"
      },
      {
        "id": 12,
        "name": "Star Wars: Episode IV - A New Hope"
      }
    ]
  }
}
```
Maybe you only want a specific property **(request body only from now on)**
```
  {
    movie(id: 11) {
      id name
    }
  }
```
Will return the following result.
```
{
  "data": {
    "movie": {
      "id": 11,
      "name": "Inception"
    }
  }
}
```
If you need other fields or relations, just ask
```
  {
    movies {
      id
      name
      director {
        name
      }
      writers {
        name
      }
    }
  }
```
Will return the following result.
```
{
  "data": {
    "movies": [
      {
        "id": 11,
        "name": "Inception",
        "director": {
          "name": "Christopher Nolan"
        },
        "writers": [{
          "name": "Christopher Nolan"
        }]
      },
      {
        "id": 12,
        "name": "Star Wars: Episode IV - A New Hope",
        "director": {
          "name": "George Lucas"
        },
        "writers": [{
          "name": "George Lucas"
        }]
      }
    ]
  }
}
```

# Custom Controller / Manual Execution

You can execute GraphQL queries in your own controller or outside of ASP.NET. Below gives an example.

## Build the GraphQL schema

We can use the helper method `SchemaBuilder.FromObject<T>>()` to build the schema from the .NET object model we defined above.

```
var schema = SchemaBuilder.FromObject<DemoContext>();
```

_See the Schema Creation section to learn more about `SchemaBuilder.FromObject<T>>()`_


## Executing a Query

Here is an example of a controller that receives a `QueryRequest` and executes the query. This logic could easily be applied to other web frameworks.

```
[Route("graphql")]
public class QueryController : Controller
{
    private readonly DemoContext _dbContext;
    private readonly SchemaProvider<DemoContext> _schemaProvider;

    public QueryController(DemoContext dbContext, SchemaProvider<DemoContext> schemaProvider)
    {
        this._dbContext = dbContext;
        this._schemaProvider = schemaProvider;
    }

    [HttpPost]
    public object Post([FromBody]QueryRequest query)
    {
        try
        {
            var results = _schemaProvider.ExecuteQuery(query, _dbContext, HttpContext.RequestServices, null);
            // gql compile errors show up in results.Errors
            return results;
        }
        catch (Exception)
        {
            return HttpStatusCode.InternalServerError;
        }
    }
}
```

It is recommended to use [Newtonsoft.Json](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.NewtonsoftJson) when using using .NET Core 3.1 due to problems with the System.Text.Json serialization in .NET Core 3.1 and dictionaries.

You can use System.Text.Json with .NET Core 5.0+. If you use your own controller to execute GraphQL, configure System.Text.Json like so.

```c#
services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.JsonSerializerOptions.IncludeFields = true;
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
```
