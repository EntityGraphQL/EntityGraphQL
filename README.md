# Entity Query Language
EQL is a data/object querying language for .NET Core.

Full GraphQL query syntax support is being worked on. Although it is already quite powerful using familar Linq-style queries.

EQL allows you to quickly expose an object-graph as an API in your applications. It can also be used to execute expressions at runtime against a given object which provides powerful runtime configuration.

_Please explore, give feedback or join the development._

## Serving your data
The ``EntityQueryLanguage.GraphQL`` namespace contains the GraphQL-like object querying.

### Getting up and running with EF

_Note: Queries are compiled to `IQueryable` linq expressions. EF is not a requirement - any ORM should work - although EF is tested well._

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
2. Create a route
Using what ever API library you wish. Here is an example for a ASP.NET WebApi controller

```csharp
public class Startup {
  public void ConfigureServices(IServiceCollection services)
  {
      services.AddDbContext<MyDbContext>(opt => opt.UseInMemoryDatabase());
      // add schema provider so we don't need to create it everytime
      // Also for this demo we expose all fields on MyDbContext. See below for details on building custom fields etc.
      services.AddSingleton(SchemaBuilder.FromObject<MyDbContext>());
  }
}

[Route("api/[controller]")]
public class QueryController : Controller
{
    private readonly MyDbContext _dbContext;
    private readonly MappedSchemaProvider<MyDbContext> _schemaProvider;

    public QueryController(MyDbContext dbContext, MappedSchemaProvider<MyDbContext> schemaProvider)
    {
        this._dbContext = dbContext;
        this._schemaProvider = schemaProvider;
    }

    [HttpGet]
    public object Get(string query)
    {
        return RunDataQuery(query);
    }

    [HttpPost]
    public object Post([FromBody]string query)
    {
        return RunDataQuery(query);
    }

    private object RunDataQuery(string query)
    {
        try
        {
            var data = _dbContext.QueryObject(query, _schemaProvider, relationHandler: new EfRelationHandler(typeof(EntityFrameworkQueryableExtensions)));
            if (data.ContainsKey("error"))
                return this.StatusCode(StatusCodes.Status500InternalServerError, data);

            return data;
        }
        catch (Exception)
        {
            return HttpStatusCode.InternalServerError;
        }
    }
}
```
`EfRelationHandler` is a helper class to handle EFs `.Include()` calls. `EntityQueryLanguage.GraphQL` does not have a requirement on EF (But this example does).

This sets up 2 end points:
- `POST` at `/api/query` where the query is in the body of the post
- `GET` at `/api/query` where the query is expected as the `q` parameter. e.g. `GET /api/query?q={locations {name}}`

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
  {
    properties.where(id = 11) {
      id, name
    }
  }
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
  {
    properties {
      id,
      name,
      location {
        name
      },
      type {
        premium
      }
    }
  }
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

As mentioned, EQL just compiles to .NET LINQ functions (IQueryable extension methods - Where() and friends) so you could use this with any ORMs/LinqProviders or libraries but it currently is only tested against EntityFramework Core 1.1.

### Supported GraphQL features
- Fields - the core part, select the fields you want returned, including selecting the fields of sub-objects in the object graph
- Aliases (`{ cheapProperties: properties.where(cost < 100) { id, name } }`)
- Arguments
  - By default `` generates a non-pural field for any type with a public `Id` property
  - See `schemaProvider.AddField("name", paramTypes, selectionExpression, "description");` in Customizing the schema below

### Supported LINQ methods (non-GraphQL compatible)
- `array.where(filter)`
- `array.filter(filter)`
- `array.first(filter?)`
- `array.last(filter?)`
- `array.count(filter?)`
  - `filter` is an expression that can be `true` or `false`, written from the context of the array item
- `array.take(int)`
- `array.skip(int)`
- `array.orderBy(field)`
- `array.orderByDesc(field)`

### Customizing the schema

You can customise the default schema, or create one from stratch exposing only the fields you want.
```csharp
var schema = SchemaBuilder.FromObject<MyDbContext>();

// custom fields on existing type
schema.Type<Person>().AddField("totalChildren", p => p.Children.Count(), "Number of children");

// custom type
schema.AddType<TBaseEntity>("name", "description");
// e.g. add a new type based on Person filtered by an expression
var type = schema.AddType<Person>("peopleOnMars", "All people on mars", person => person.Location.Name == "Mars");
type.AddPublicProperties(); // add the C# properties
// or select the fields
type.AddField(p => p.Id, "The unique identifier");
// Add fields with arguments
schemaProvider.AddField("user", new {id = 0}, (ctx, param) => ctx.Users.FirstOrDefault(u => u.Id == param.id), "description");

// Here the type schema of the parameters are defined with the anonymous type allowing you to write the selection query with compile time safety
// You can also use default()
var paramTypes = new {id = default(Guid)};
```

### Secuity

TODO - coming soon

## Using expressions else where
Lets say you have a screen in your application listing properties that a user can configure to only show exactly what they are interested in. Instead of having a bunch of checkboxes and complex radio buttons etc. you can allow a simple EQL statement to configure the results shown.
```js
  // This might be a configured EQL statement for filtering the results. It has a context of Property
  (type.id = 2 or type.id = 3) and type.name startswith 'Region'
```
This would compile to `(Property p) => (p.Type.Id == 2 || p.Type.Id == 3) && p.Type.Name.StartsWith("Region");`

This can then be used in various Linq functions either in memory or against an ORM.
```csharp
// we create a schema provider to compile the statement against our Property type
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

- Implement more of the GraphQL query spec
  - fragments
  - operation names
  - variables
  - Directives
  - Mutations
  - Inline fragments
  - meta fields
- fix GetMethodContext() in methodProvider
- Extend schema type system
- Add support for data manipulation - adds, updates, deletes
- Add logging options
- A way to "plug-in" security - examples
- A way to "plug-in" business logic - examples
- Auto generate schema documentation page
- better paging (from graphql?)
- Authentication and access control options
