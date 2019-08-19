# Entity GraphQL
Note: `master` is currently not stable.

Build status: [![CircleCI](https://circleci.com/gh/lukemurray/EntityGraphQL/tree/master.svg?style=svg)](https://circleci.com/gh/lukemurray/EntityGraphQL/tree/master)

Entity GraphQL is a .NET Core (netstandard 1.6) library that allows you to query your data using the GraphQL syntax.

It can also be used to execute simple LINQ-style expressions at runtime against a given object which provides powerful runtime configuration.

_Please explore, give feedback or join the development._

## Install
Via Nuget https://www.nuget.org/packages/EntityGraphQL

# Getting up and running with EF

_Note: There is no dependency on EF. Queries are compiled to `IQueryable` or `IEnumberable` linq expressions. EF is not a requirement - any ORM working with `LinqProvider` or an in-memory object should work - although EF well is tested._

## 1. Define your data context (in this example an EF context)

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
## 2. Create a route

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
    public object Post([FromBody]QueryRequest query)
    {
        try
        {
            var results = _dbContext.QueryObject(query, _schemaProvider);
            // gql compile errors show up in results.Errors
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

# Supported GraphQL features
- Fields - the core part, select the fields you want, including selecting the fields of sub-objects in the object graph
- Aliases (`{ cheapProperties: properties(maxCost: 100) { id name } }`)
- Arguments
  - Add fields that take required or optional arguments to fullfill the query
  - By default `SchemaBuilder.FromObject<TType>()` generates a non-pural field for any type with a public `Id` property, with the argument name of `id`. E.g. A field `people` that returns a `IEnumerable<Person>` will create a `person(id)` graphql field so you can query `{ person(id: 1234) { name email } }` to select a single person
  - See `schemaProvider.AddField("name", paramTypes, selectionExpression, "description");` in "Customizing the schema" below for more on custom fields
- Mutations - see `AddMutationFrom<TType>(TType mutationClassInstance)` and details below under Mutation
- Schema introspection

# Customizing the schema

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

## LINQ Helper Methods
EntityGraphQL provides a few extension methods to help with building queries with optional parameters.

- `Take(int?)` - Only apply the `Take()` method if the argument has a value. Usage: `schema.AddField("Field", new { limit = (int?)null }, (db, p) => db.Entity.Take(p.limit), "description")`
- `WhereWhen(predicate, when)` - Only apply the `Where()` method is `when` is true. Usage: `schema.AddField("Field", new { search = (string)null }, (db, p) => db.Entity.WhereWhen(s => s.Name.ToLower().Contains(p.search.ToLower()), !string.IsNullOrEmpty(p.search)), "Description")`

# Mutations
Mutations allow you to make changes to data while selecting some data to return from the result. See the [GraphQL documentation](https://graphql.org/learn/queries/#mutations) for more information on the syntax.

The main concept behind this is you create a class (or many) to encapsulate all your mutations. This lets you break them up into multiple classes by functionality or just entity type.

Although you can just return an object and the GraphQL query will be executed against that object. The suggested way is to return an `Expression`. This lets you access deep levels of the object model that may not be loaded in memory in the case you are using an ORM.

I.e if you have a mutation adds an actor to a movie entity and you want to return the _current_ list of full actors.

```csharp
public class MovieMutations
{
  [GraphQLMutation]
  public Movie AddActor(MyDbContext db, ActorArgs args)
  {
    // do your magic here. e.g. with EF or other business logic
    var movie = db.Movies.First(m => m.Id == args.Id);
    var actor = new Person { Name = args.Name, ... };
    movie.Actors.Add(actor);
    db.SaveChanges();
    return movie;
  }
}

public class PropertyArgs
{
  public string Name { get; set; }
  public Decimal Cost { get; set; }
}
```

Not here, we did not `Include()` the current actors. If the query of the GraphQL mutation was `{ name actors { name id } }`, we would only get data that is loaded in memory of the `movie` variable.

To support access to the full graph, regardless of if it is already loaded or via an ORM (like EF). It is reccomended to return an expression like so.

```csharp
public class MovieMutations
{
  [GraphQLMutation]
  public Expression<Func<MyDbContext, Movie>> AddActor(MyDbContext db, ActorArgs args)
  {
    // do your magic here. e.g. with EF or other business logic
    var movie = db.Movies.First(m => m.Id == args.Id);
    var actor = new Person { Name = args.Name, ... };
    movie.Actors.Add(actor);
    db.SaveChanges();
    return ctx => ctx.Movies.First(m => m.Id == movie.Id);
  }
}
```

Note the return signature change and the result we return is a `Func` that selects the movie we just modified.

To add this mutation to your schema, call

```csharp
schemaProvider.AddMutationFrom(new MovieMutations());
```

- All `public` methods marked with the `GraphQLMutation` attribute will be added to the schema
- Parameters for the method should be
  - First - the base context that your schema is built from
  - Optionally, any other items you have passed to `QueryObject` (see below for example)
  - Last - a class that defines each available parameter (and type)
- Variables from the GraphQL request are mapped into the args (last) parameter

You can now request a mutation
```
mutation AddActor($name: String!, $movieId: int!) {
  addMovie(name: $name, id: $movieId) {
    id name
    actors { name id }
  }
}
```

With variables
```json
{
  "name": "Robot Dophlin",
  "movieId": 2
}
```

## Accessing other services in your mutation
`QueryObject` supports `mutationArgs` as parameters which can be 0+ variables that will be resolved to your mutation method.

A big use case is `IServiceProvider`. When you call `QueryObject` you can pass any number of other variables in e.g. `var data = dbContext.QueryObject(gql, schemaProvider, serviceProvider);`

If you define a mutation method that requires that parameter type it will be resolved to the value you passed `QueryObject`. Note EntityGraphQL will not use `IServiceProvider` to resolve _any_ parameter. This is just an example of getting the `IServiceProvider` to your mutation for those who use it.

```csharp
public class MovieMutations
{
  [GraphQLMutation]
  public Expression<Func<MyDbContext, Movie>> AddActor(MyDbContext db, IServiceProvider serviceProvider, ActorArgs args)
  {
    var myService = serviceProvider.GetService<...>();
    myService.DoSomething();
    return ctx => ctx.Movies.First(m => m.Id == movie.Id);
  }
}
```

# A note on case matching

GraphQL is case sensitive. Currently EntityGraphQL will automatically turn "fields" from `UpperCase` to `camelCase` which means your C# code matches what C# code typically looks like and your graphql matches the norm too.

Examples:
- A mutation method in C# named `AddMovie` will be `addMovie` in the schema
- A root field entity named `Movie` will be named `movie` in the schema
- The mutation arguments class (`ActorArgs` above) with fields `FirstName` & `Id` will be arguments in the schema as `firstName` & `id`
- If you're using the schema builder manually, the names you give will be the names used. E.g. `schemaProvider.AddField("someEntity", ...)` is different to `schemaProvider.AddField("SomeEntity", ...)`

# Secuity

## Mutations
Security checks should be done in your mutation code.

## Queries
Coming soon. But you should have security at other layers too

# Paging
For paging you want to create your own fields.

```cs
schemaProvider.AddField("myEntities", new {take = 10, skip = 0}, (db, param) => db.MyEntities.Skip(p.skip).Take(p.take), "Get a page of entities");
```

Open to ideas for making this easier.

# Intergrating with other tools
Many tools can help you with typing or generating code from a GraphQL schema. Use `schema.GetGraphQLSchema()` to produce a GraphQL schema file. This works well as input to the Apollo code gen tools.

# Using expressions else where (EQL)
Lets say you have a screen in your application listing properties that can be configured per customer or user to only show exactly what they are interested in. Instead of having a bunch of checkboxes and complex radio buttons etc. you can allow a simple EQL statement to configure the results shown. Or use those UI components to build the query.
```cs
  // This might be a configured EQL statement for filtering the results. It has a context of Property
  (type.id = 2 or type.id = 3) and type.name = "Farm"
```
This would compile to `(Property p) => (p.Type.Id == 2 || p.Type.Id == 3) && p.Type.Name == "Farm";`

This can then be used in various Linq functions either in memory or against an ORM.
```csharp
// we create a schema provider to compile the statement against our Property type
var schemaProvider = SchemaBuilder.FromObject<Property>();
var compiledResult = EqlCompiler.Compile(myConfigurationEqlStatement, schemaProvider);
// you get your list of Properties from you DB
var thingsToShow = myProperties.Where(compiledResult.LambdaExpression);
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

# Contribute
Please do. Pull requests are very welcome. See the open issues for bugs or features that would be useful
