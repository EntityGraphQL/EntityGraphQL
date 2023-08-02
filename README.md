# Entity GraphQL

## A GraphQL library for .NET Core

![Build](https://github.com/lukemurray/EntityGraphQL/actions/workflows/dotnet.yml/badge.svg)

Jump into the [https://entitygraphql.github.io/](https://entitygraphql.github.io/) for documentation and to get started.

EntityGraphQL is a .NET library that allows you to easily build a [GraphQL API](https://graphql.org/learn/) on top of your data model with the extensibility to easily bring multiple data sources together in the single GraphQL schema.

EntityGraphQL builds a GraphQL schema that maps to .NET objects. It provides the functionality to parse a GraphQL query document and execute that against your mapped objects. These objects can be an Entity Framework `DbContext` or any other .NET object, it doesn't matter.

A core feature of EntityGraphQL _with_ Entity Framework (although EF is not a requirement) is that it builds selections of only the fields requested in the GraphQL query which means Entity Framework is not returning all columns from a table. This is done with the [LINQ](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/) projection operator [`Select()`](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/projection-operations#select) hence it works across any object tree.

_Please explore, give feedback or join the development._

## Installation

The [EntityGraphQL.AspNet ![Nuget](https://img.shields.io/nuget/dt/EntityGraphQL.AspNet)](https://www.nuget.org/packages/EntityGraphQL.AspNet) package will get you easily set up with ASP.NET. 

However the core [EntityGraphQL ![Nuget](https://img.shields.io/nuget/dt/EntityGraphQL)](https://www.nuget.org/packages/EntityGraphQL) package has no ASP.NET dependency.

# Quick Start with Entity Framework

_Note: There is no dependency on EF. Queries are compiled to `IQueryable` or `IEnumberable` linq expressions. EF is not a requirement - any ORM working with `LinqProvider` or an in-memory object will work - although EF well is tested._

## 1. Define your data context (in this example an EF context)

```c#
public class DemoContext : DbContext {
  public DemoContext(DbContextOptions options) : base(options)
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

Here is an example for a ASP.NET. You will also need to install EntityGraphQL.AspNet to use `MapGraphQL`. You can also build you own endpoint, see docs.

[![Nuget](https://img.shields.io/nuget/dt/EntityGraphQL.AspNet)](https://www.nuget.org/packages/EntityGraphQL.AspNet)

```c#
public class Startup {
  public void ConfigureServices(IServiceCollection services)
  {
      services.AddDbContext<DemoContext>(opt => opt.UseInMemoryDatabase());
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

This sets up 1 end point:

- `POST` at `/graphql` where the body of the post is a GraphQL query
- You can authorize that route how you would any ASP.NET route. See Authorization below for details on having parts of the schema requiring Authorization/Claims

_Note - As of version 1.1+ the EntityGraphQL.AspNet extension helper uses System.Text.Json. Previous versions used JSON.NET._

## 3. Build awesome applications

You can now make a request to your API. For example

```
  POST localhost:5000/graphql
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

Visit [documentation](https://entitygraphql.github.io/) for more information.

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

# Versioning

We do our best to follow [Semantic Versioning](https://semver.org):

Given a version number `MAJOR.MINOR.PATCH`, an increment in:

- `MAJOR` version is when we make incompatible API changes,
- `MINOR` version is when we add functionality in a backwards compatible manner, and
- `PATCH` version is when we make backwards compatible bug fixes.

# Contribute & Join the Development

Please do. Pull requests are very welcome. See the open issues for bugs or features that would be useful.
