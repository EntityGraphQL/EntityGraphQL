# Entity Query Language
EQL is a data/object querying language (should support GraphQL syntax but I started it before the spec - it should still look familar) allowing you to quickly expose an API for your applications. Applications and consumers of the API then have control over what data they query using the GraphQL syntax. It can also be used to execute expressions at runtime against a given object which provides powerful runtime configuration.

_EQL is in early stages of development. Please explore, give feedback or join the development._

## Serving your data
The ``EntityQueryLanguage.DataApi`` namespace contains some middleware to easily get up and running with an API for your application. An overview of it's features:
- Applications/consumers define what data they want, not your REST endpoints
- Meaning you don't over send data
- You can fetch complex object graphs in one go, or many
- Working in your application you know what data to expect without jumping to documentation as it clearly defined

### Getting up and running

1. Define your EF context

```csharp
public class MyDbContext : DbContext {
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
2. Configure the Data API middleware

```csharp
public class Startup {
  public void Configure(IApplicationBuilder app) {
    var options = new DataApiMiddlewareOptions {
      Schema = new ObjectSchemaProvider<MyDbContext>(),
      Path = "/api/query"
    };
    app.UseMiddleware<DataApiMiddleware<MyDbContext>>(options);
  }
}
```
3. Build awesome applications

You can now make request to your API. For example
```
  GET localhost:5000/api/query?q={properties { id, name }}
```
Will return the following result.
```json
{
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
```
Maybe you only want a specific property
```
  GET localhost:5000/api/query?q={properties.where(id = 11) { id, name }}
```
Will return the following result.
```json
{
  "properties": [
    {
      "id": 11,
      "name": "My Beach Pad"
    }
  ]
}
```
If you need a deeper graph or relations, just ask
```
  GET localhost:5000/api/query?q={properties { id, name, location { name }, type { premium } }}
```
Will return the following result.
```json
{
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
```
Technically EQL just compiles to .NET LINQ functions (IQueryable extension methods - Where() and friends) so you could use this with any ORMs/LinqProviders or libraries but it currently is only tested against EntityFramework 7.

[Check out the wiki](https://github.com/lukemurray/EntityQueryLanguage/wiki) for more detail on writing EQL expressions and data queries.

This allows you to easily get up and running building your application. It even continues to work great if you only have the one internal client app accessing the API. However if you have a public API or need to version things you can do that too.

TODO - doco here

## Using expressions else where
Lets look at an example. You have a screen in your application listing properties that a user can configure to only show exactly what they are interested in. Instead of having a bunch of checkboxes and complex radio buttons etc. you can allow a simple EQL statement to configure the results shown.
```js
  // This might be a configured EQL statement for filtering the results. It has a context of Property
  (type.id = 2 or type.id = 3) and type.name startswith 'Region'
```
This can be compile and checked for correctness at configuration time and as part of you deploy process, to make sure all EQL statements are valid. To use it in your application:
```csharp
// we create a schema provider (more on this later) to compile the statement against our Property type
var schemaProvider = new ObjectSchemaProvider<Property>();
var compiledResult = EqlCompiler.Compile(myConfigurationEqlStatement, schemaProvider);
// you get your list of Properties from you DB
var thingsToShow = myProperties.Where(compiledResult.Expression);
```
Another example is you want a customised calculated field. You can execute a compiled result passing in an instance of the context type.
```csharp
// You'd take this from some configuration
var eql = "if location.name = 'Mars' then (cost + 5) * type.premium else (cost * type.premium) / 3"
var compiledResult = EqlCompiler.Compile(eql, schemaProvider);
var theRealPrice = compiledResult.Execute<decimal>(myPropertyInstance);
```

# TODO
Some larger things still on the list to complete, in no real order. Pull requests are very welcome.

- fix GetMethodContext() in methodProvider
- Implement more of the GraphQL query spec
  - Arguments (note you can also use a LINQ style query for filtering etc.)
  - fragments
  - variables & operation names
  - Directives
  - Mutations
  - Inline fragments
  - meta fields
- Extend schema type system
- Add support for data manipulation - adds, updates, deletes
- Add logging options
- A way to "plug-in" security - examples
- A way to "plug-in" business logic - examples
- Auto generate schema documentation page
- better paging (from graphql?)
- Wiki page on writing queries
- Authentication and access control options
