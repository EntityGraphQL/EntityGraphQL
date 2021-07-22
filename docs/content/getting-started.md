---
title: "Getting Started"
metaTitle: "Getting started - EntityGraphQL"
metaDescription: "Get up and running with EntityGraphQL"
---

# Installation
Install via [Nuget](https://www.nuget.org/packages/EntityGraphQL)

_It recommended to use [Newtonsoft.Json](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.NewtonsoftJson) when using using .NET Core 3.1+ due to problems with the default serialization of dictionaries in .NET Core 3.1._

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

# Build the GraphQL schema

We can use the helper method `SchemaBuilder.FromObject<T>>()` to build the schema from the .NET object model we defined above.

```
var schema = SchemaBuilder.FromObject<DemoContext>();
```

_See the Schema Creation section to learn more about `SchemaBuilder.FromObject<T>>()`_

Below we'll use this to expose an API with ASP.NET. See the next section on manually creating the schema and then Schema Customization for further customizations supported on the schema.

# Create the API

Using what ever .NET API library you wish you can receive a query, execute it and return the data. Here is an example with ASP.NET Core.

```
public class Startup {
  public void ConfigureServices(IServiceCollection services)
  {
      services.AddControllers().AddNewtonsoftJson();
      services.AddDbContext<DemoContext>(opt => opt.UseInMemoryDatabase()); // Again this example using EF but you do not have to
      // add schema provider so we don't need to create it every time
      // Also for this demo we expose all fields on DemoContext
      services.AddSingleton(SchemaBuilder.FromObject<DemoContext>());
  }
}

[Route("api/[controller]")]
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

This sets up a `HTTP` `POST` end point at `/api/query` where the body of the post is expected to be a GraphQL query.

_You can authorize the route how ever you wish using ASP.NET. See the Authorization section for more details._

You can also expose any endpoint over any protocol you'd like. We'll use HTTP/S for these examples.

# Query your API

You can now make a request to your API via any HTTP tool/library.

For example
```
  POST localhost:5000/api/query
  {
    properties { id name }
  }
```

Will return the following result (depending on the data in you DB).

```json
{
  "data": {
    "properties": [
      {
        "id": 11,
        "name": "My Beach Pad"
      },
      {
        "id": 12,
        "name": "My Other Beach Pad"
      }
    ]
  }
}
```
Maybe you only want a specific property **(request body only from now on)**
```
  {
    property(id: 11) {
      id name
    }
  }
```
Will return the following result.
```json
{
  "data": {
    "property": {
      "id": 11,
      "name": "My Beach Pad"
    }
  }
}
```
If you need other fields or relations, just ask
```
  {
    properties {
      id
      name
      location {
        name
      }
      type {
        premium
      }
    }
  }
```
Will return the following result.
```json
{
  "data": {
    "properties": [
      {
        "id": 11,
        "name": "My Beach Pad",
        "location": {
          "name": "Greece"
        },
        "type": {
          "premium": 1.2
        }
      },
      {
        "id": 12,
        "name": "My Other Beach Pad",
        "location": {
          "name": "Spain"
        },
        "type": {
          "premium": 1.25
        }
      }
    ]
  }
}
```