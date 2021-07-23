# Entity GraphQL
## A GraphQL library for .NET Core

![Build](https://github.com/lukemurray/EntityGraphQL/actions/workflows/dotnet.yml/badge.svg)
[![Nuget](https://img.shields.io/nuget/dt/EntityGraphQL)](https://www.nuget.org/packages/EntityGraphQL)

Jump into the [documentation](https://lukemurray.github.io/EntityGraphQL) to get started.

Entity GraphQL is a .NET Core (netstandard 1.6) library that allows you to easily build a GraphQL API on top of your data with the extensibility to bring multiple data sources together in the single GraphQL schema.

It can also be used to execute simple LINQ-style expressions at runtime against a given object which provides powerful runtime configuration.

_Please explore, give feedback or join the development._

If you're looking for a dotnet library to generate code to query an API from a GraphQL schema see https://github.com/lukemurray/DotNetGraphQLQueryGen

## Installation
Via Nuget

[![Nuget](https://img.shields.io/nuget/dt/EntityGraphQL)](https://www.nuget.org/packages/EntityGraphQL)

It recommended to use [Newtonsoft.Json](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.NewtonsoftJson) when using using .NET Core 3.1+ due to problems with the default serialization in .NET Core 3.1.

# Quick Start with Entity Framework

_Note: There is no dependency on EF. Queries are compiled to `IQueryable` or `IEnumberable` linq expressions. EF is not a requirement - any ORM working with `LinqProvider` or an in-memory object will work - although EF well is tested._

## 1. Define your data context (in this example an EF context)

```c#
public class MyDbContext : DbContext {
  public MyDbContext(DbContextOptions options) : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder builder) {
    // Set up your relations
  }

  public DbSet<Property> Properties { get; set; }
  public DbSet<PropertyType> PropertyTypes { get; set; }
  public DbSet<Location> Locations { get; set; }
}

public class Property {
  public uint Id { get; set; }
  public string Name { get; set; }
  public PropertyType Type { get; set; }
  public Location Location { get; set; }
}

public class PropertyType {
  public uint Id { get; set; }
  public string Name { get; set; }
  public decimal Premium { get; set; }
}

public class Location {
  public uint Id { get; set; }
  public string Name { get; set; }
}
```
## 2. Create a route

Using what ever API library you wish. Here is an example for a ASP.NET Core WebApi controller.

```c#
public class Startup {
  public void ConfigureServices(IServiceCollection services)
  {
      services.AddControllers().AddNewtonsoftJson();
      services.AddDbContext<MyDbContext>(opt => opt.UseInMemoryDatabase());
      // add schema provider so we don't need to create it every time
      // Also for this demo we expose all fields on MyDbContext. See below for details on building custom fields etc.
      services.AddSingleton(SchemaBuilder.FromObject<MyDbContext>());
  }
}

[Route("api/[controller]")]
public class QueryController : Controller
{
    private readonly MyDbContext _dbContext;
    private readonly SchemaProvider<MyDbContext> _schemaProvider;

    public QueryController(MyDbContext dbContext, SchemaProvider<MyDbContext> schemaProvider)
    {
        this._dbContext = dbContext;
        this._schemaProvider = schemaProvider;
    }

    [HttpPost]
    public object Post([FromBody]QueryRequest query)
    {
        try
        {
            var results = _schemaProvider.ExecuteQuery(query, _dbContext, null, null);
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

This sets up 1 end point:
- `POST` at `/api/query` where the body of the post is a GraphQL query
- You can authorize that route how you would any ASP.NET route. See Authorization below for details on having parts of the schema requiring Authorization/Claims

## 3. Build awesome applications

You can now make a request to your API. For example
```
  POST localhost:5000/api/query
  {
    properties { id name }
  }
```
Will return the following result.
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
Maybe you only want a specific property
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
If you need a deeper graph or relations, just ask
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

Visit [documentation](https://lukemurray.github.io/EntityGraphQL) for more information.

# Using expressions else where (EQL)
Lets say you have a screen in your application listing properties that can be configured per customer or user to only show exactly what they are interested in. Instead of having a bunch of checkboxes and complex radio buttons etc. you can allow a simple EQL statement to configure the results shown. Or use those UI components to build the query.
```cs
  // This might be a configured EQL statement for filtering the results. It has a context of Property
  (type.id = 2) or (type.id = 3) and type.name = "Farm"
```
This would compile to `(Property p) => (p.Type.Id == 2 || p.Type.Id == 3) && p.Type.Name == "Farm";`

This can then be used in various Linq functions either in memory or against an ORM.
```csharp
// we create a schema provider to compile the statement against our Property type
var schemaProvider = SchemaBuilder.FromObject<Property>();
var compiledResult = EntityQueryCompiler.Compile(myConfigurationEqlStatement, schemaProvider);
// you get your list of Properties from you DB
var thingsToShow = myProperties.Where(compiledResult.LambdaExpression);
```

Another example is you want a customised calculated field. You can execute a compiled result passing in an instance of the context type.
```csharp
// You'd take this from some configuration
var eql = @"if location.name = ""Mars"" then (cost + 5) * type.premium else (cost * type.premium) / 3"
var compiledResult = EntityQueryCompiler.Compile(eql, schemaProvider);
var theRealPrice = compiledResult.Execute<decimal>(myPropertyInstance);
```

# Contribute & Join the Development
Please do. Pull requests are very welcome. See the open issues for bugs or features that would be useful.
