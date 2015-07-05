# Entity Query Language
EQL is a powerful data querying language which lets you quickly build an API backend for your applications. Application and consumers of the API then have control over what data they query using a GraphQL inspired syntax. It is built on top of a simple language allowing you to easily build .NET expressions for execution against a given context object which can also be used for powerful runtime configuration.

_Note EQL is in early stages of development_

## How do I use this?
Lets look at an example. You have a screen in your application listing properties that a user can configure to only show exactly what they are interested in. Instead of having a bunch of checkboxes and complex radio buttons etc. you can allow a simple EQL statement to configure the results shown.
```js
  // This might be a configured EQL statement for filtering the results. It has a context of Property
  (type.id = 2 or type.id = 3) and type.name startswith 'Region'
```
This can be compile and checked for correctness at configuration time and as part of you deploy process, to make sure all EQL statements are valid. To use it in your application:
```csharp
var compiler = new EqlCompiler();
// we create a schema provider (more on this later) to compile the statement against our Property type
var schemaProvider = new ObjectSchemaProvider(typeof(Property));
var compiledResult = compiler.Compile(myConfigurationEqlStatement, schemaProvider);
// you get your list of Properties from you DB
var thingsToShow = myProperties.Where(compiledResult.Expression);
```
Another example is you want a customised calculated field. You can execute a compiled result passing in an instance of the context type.
```csharp
// You'd take this from some configuration
var eql = "if location.name = 'Mars' then (cost + 5) * type.premium else (cost * type.premium) / 3"
var compiledResult = compiler.Compile(eql, schemaProvider);
var theRealPrice = compiledResult.Execute<decimal>(myPropertyInstance);
```
## Building an API
The ``EntityQueryLanguage.DataApi`` namespace contains some middleware to easily get up and running with an API for your application. An overview of it's features:
- Applications/consumers define what data they want, not you REST endpoints
- Meaning you don't over send data
- You can fetch complex object graphs in one go, or many
- Working in your application you know what data to expect without jumping to documentation as it clearly defined

The Data API syntax has been inspired by Facebook's GraphQL, for a nice overview of GraphQL and why this is a powerful way to query your data check out Facebook's video [here](https://www.youtube.com/watch?v=9sc8Pyc51uU).

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
1. Configure the Data API middleware

```csharp
public class Startup {
  public void Configure(IApplicationBuilder app) {
    var options = new DataApiMiddlewareOptions {
      Schema = new ObjectSchemaProvider(typeof(MyDbContext), () => new MyDbContext()),
      Path = "/api"
    };
    app.UseMiddleware<DataApiMiddleware<HighPlanContext>>(options);
  }
}
```
1. Build awesome applications

You can now make request to your API. For example
```
  GET localhost:5000/api?query={properties { id, name }}
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
  GET localhost:5000/api?query={properties.where(id = 11) { id, name }}
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
If you need a deeper graph or relations, just tell us
```
  GET localhost:5000/api?query={properties { id, name, location { name }, type { premium } }}
```
Will return the following result.
```json
{
  "properties": [
    {
      "id": 11,
      "name": "My Beach Pad",
      "location": { "name": "Greece" },
      "type": { "premium": 1.2 }
    },
    {
      "id": 12,
      "name": "My Other Beach Pad",
      "location": { "name": "Spain" },
      "type": { "premium": 1.25 }
    }
  ]
}
```
Technically EQL just compiles to .NET LINQ functions (Where() and friends) so you could use this with other ORMs or libraries but it currently is only tested against EntityFramework 7.

[Check out the wiki](https://github.com/lukemurray/EntityQueryLanguage/wiki) for more detail on writing EQL expressions and data queries.

# TODO
Some things still on the list to complete. Pull request are very welcome.

- EF navigation not implemented (https://github.com/aspnet/EntityFramework/issues/325)

- alias' in queries (myName: fieldExpression { })
- parameters in queries e.g. defining the size of an image to return
- Add logging options
- fix GetMethodContext()
- Support for a schema definition for versioning - JSON or maybe CS?
- Add support for data manipulation - writes, updates, deletes
- A way to "plug-in" security - examples
- A way to "plug-in" business logic - examples
- Auto generate schema documentation page
- better paging
- Wiki page on writing EQL
