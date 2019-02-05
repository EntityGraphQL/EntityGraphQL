# Entity GraphQL
Build status: [![CircleCI](https://circleci.com/gh/lukemurray/EntityGraphQL/tree/master.svg?style=svg)](https://circleci.com/gh/lukemurray/EntityGraphQL/tree/master)

Entity GraphQL is a .NET Core (netstandard 1.6) library that allows you to query your data using the GraphQL syntax.

It can also be used to execute simple LINQ-style expressions at runtime against a given object which provides powerful runtime configuration.

_Please explore, give feedback or join the development._

## Install
Via Nuget https://www.nuget.org/packages/EntityGraphQL

## Getting up and running with EF

_Note: There is no dependency on EF. Queries are compiled to `IQueryable` or `IEnumberable` linq expressions. EF is not a requirement - any ORM working on LinqProvider on an in-memory object should work - although EF well is tested._

### 1. Define your data context (in this case an EF context)

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
### 2. Create a route

Using what ever API library you wish. Here is an example for a ASP.NET Core WebApi controller

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

    [HttpPost]
    public object Post([FromBody]string query)
    {
        try
        {
            var results = _dbContext.QueryObject(query, _schemaProvider);
            // most gql compile errors show up in results["errors"]
            return results
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

### 3. Build awesome applications

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

## Supported GraphQL features
- Fields - the core part, select the fields you want, including selecting the fields of sub-objects in the object graph
- Aliases (`{ cheapProperties: properties(maxCost: 100) { id name } }`)
- Arguments
  - Add fields that take required or optional arguments to fullfill the query
  - By default `SchemaBuilder.FromObject<TType>()` generates a non-pural field for any type with a public `Id` property, with the argument name of `id`. E.g. A field `people` that returns a `IEnumerable<Person>` will create a `person(id)` graphql field so you can query `{ person(id: 1234) { name email } }` to select a single person
  - See `schemaProvider.AddField("name", paramTypes, selectionExpression, "description");` in "Customizing the schema" below for more on custom fields
- Mutations - see `AddMutationFrom<TType>(TType mutationClassInstance)` and details below under Mutation

## Customizing the schema

You can customise the default schema, or create one from stratch exposing only the fields you want.
```csharp
// Build from object
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
// Add fields with _required_ arguments - include `using static EntityGraphQL.Schema.ArgumentHelper;`
schemaProvider.AddField("user", new {id = Required<int>()}, (ctx, param) => ctx.Users.FirstOrDefault(u => u.Id == param.id), "description");

// Here the type schema of the parameters are defined with the anonymous type allowing you to write the selection query with compile time safety
// You can also use default()
var paramTypes = new {id = Required<Guid>()};

// If you use a value, the argument will not be required and the value used as a default
var paramTypes = new {unit = "meter"};
```

## Mutations
Mutations allow you to make changes to data while selecting some data to return from the result. See the [GraphQL documentation](https://graphql.org/learn/queries/#mutations) for more information on the syntax.

The main concept behind this is you create a class (or many) to encapsulate all your mutations. This lets you break them up into multiple classes by functionality or just entity type.

```csharp
public class PropertyMutations
{
  [GraphQLMutation]
  public Property AddProperty(MyDbContext db, PropertyArgs args)
  {
    // do your magic here. e.g. with EF
    var property = new Property { Name = args.Name, ... };
    db.Properties.Add(property);
    db.SaveChanges();
    return property;
  }
}

public class PropertyArgs
{
  public string Name { get; set; }
  public Decimal Cost { get; set; }
}
```

To add this to you schema, call

```csharp
schemaProvider.AddMutationFrom(new PropertyMutations());
```

- All `public` methods marked with the `GraphQLMutation` attribute will be added to the schema
- Parameters should be the base context that your schema is built from and a class that defines each available parameter (and type)
- Variables from the GraphQL request are mapped into the args (second) parameter

You can now request a mutation
```
mutation AddProperty($name: String!, $cost: Float!) {
  addProperty(name: $name, cost: $cost) {
    id
    name
  }
}
```

With variables
```json
{
  "name": "beach pad",
  "cost": 1000000.3
}
```

This also selects the resulting `id` & `name` from the result of the mutation.

## Secuity

### Mutations
Security checks should be done in your mutation code.

### Queries
Coming soon. But you should have security at other layers too

## Paging
For paging you want to create your own fields.

```cs
schemaProvider.AddField("MyEntities", new {take = 10, skip = 0}, (db, param) => db.MyEntities.Skip(p.skip).Take(p.take), "Get a page of entities");
```

Helpers for different types of paging will likely be added.

# Intergrating with other tools
Many tools can help you with typing or generating code from a GraphQL schema. Use `schema.GetGraphQLSchema()` to produce a GraphQL schema file. This works well as input to the Apollo code gen tools.

# Using expressions else where
Lets say you have a screen in your application listing properties that can be configured per customer or user to only show exactly what they are interested in. Instead of having a bunch of checkboxes and complex radio buttons etc. you can allow a simple EQL statement to configure the results shown. Or use those UI components to build the query.
```cs
  // This might be a configured EQL statement for filtering the results. It has a context of Property
  (type.id = 2 or type.id = 3) and type.name == "Farm"
```
This would compile to `(Property p) => (p.Type.Id == 2 || p.Type.Id == 3) && p.Type.Name == "Farm";`

This can then be used in various Linq functions either in memory or against an ORM.
```csharp
// we create a schema provider to compile the statement against our Property type
var schemaProvider = SchemaBuilder.FromObject<Property>();
var compiledResult = EqlCompiler.Compile(myConfigurationEqlStatement, schemaProvider);
// you get your list of Properties from you DB
var thingsToShow = myProperties.Where(compiledResult.Expression);
```

Another example is you want a customised calculated field. You can execute a compiled result passing in an instance of the context type.
```csharp
// You'd take this from some configuration
var eql = @"if location.name = ""Mars"" then (cost + 5) * type.premium else (cost * type.premium) / 3"
var compiledResult = EqlCompiler.Compile(eql, schemaProvider);
var theRealPrice = compiledResult.Execute<decimal>(myPropertyInstance);
```

## Supported LINQ methods (non-GraphQL compatible)
**On top of** GraphQL syntax, any list/array supports some of the standard .NET LINQ methods.
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

e.g.
```
query {
  cheap2BedPlaces: peroperties.where(price < 100000 && bedrooms >= 2).orderby(age) {
    location { name }
    price
  }
}
```

# TODO
Some larger things still on the list to complete, in no real order. Pull requests are very welcome.

- fix GetMethodContext() in methodProvider
- Add logging options
- Auto generate schema documentation page
- Support integration into security for controlling data access
