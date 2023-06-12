---
sidebar_position: 1
---

# Fields

GraphQL supports [arguments](https://graphql.org/learn/queries/#arguments) on query fields. We saw this already with the `SchemaBuilder.FromObject()` helper method. It created a field with an `id` argument to select a single item by id. Of course you can create fields with your own arguments to expand the functionality of you GraphQL APi.

## Adding a field with an argument

Arguments are defined using an anonymous object type when defining the field. Let's build the `id` example ourselves.

```cs
// empty schema
var schema = new SchemaProvider<DemoContext>();
schema.AddType<Person>("Person", "Information about a person);

// add our root-level query field with an argument
schema.Query().AddField(
    "person",
    new {
        id = ArgumentHelper.Required<int>()
    },
    (ctx, args) => ctx.People.FirstOrDefault(p => p.Id == args.id),
    "Fetch person by ID"
);
```

Let's break this down.

- Each property of the anonymous object will be an argument on the GraphQL field. In this case we have 1 argument named `id`
- `ArgumentHelper.Required<T>()` lets EntityGraphQL know that the argument is required and will error if it is not supplied in the query
- The field resolve expression now has 2 parameters, the original context and the anonymous object type which at execution time will contain the argument values
- We use the `args.id` to filter the list of people to a single person that will be returned

It is worth noting that the GraphQL name of the argument will be that of the property on the anonymous type. In GraphQL arguments typically start with lower case hence we use `id`. If you used `Id` as the argument name in the anonymous object you will need to use `Id` in queries (GraphQL is case sensitive).

### Required arguments

As you see above to make an argument required use the `ArgumentHelper.Required<T>()` method.

See [Validation](../validation) for further information.

### Optional arguments

If you want the argument to be optional just set the field to a typed `null` value.

```cs
schema.Query().AddField(
    "people",
    new {
        firstName = (string)null
    },
    (ctx, args) => !string.IsNullOrEmpty(args.firstName) ? ctx.People.Where(p => p.FirstName == args.firstName) : ctx.People,
    "List of people optionally filtered by a first name"
);
```

If the argument is not supplied with a value in the query `args.firstName` will be null. We can use that to optionally filter the data.

### Default values

In GraphQL field arguments can have default values. In EntityGraphQL you just set the default value in the anonymous object create. If fact that is what we did above with `null`.

```cs
schema.Query().AddField(
    "people",
    new {
        deleted = false // default value
    },
    (ctx, args) => ctx.People.Where(p => p.IsDeleted == args.deleted),
    "List all active people. Optional list all deleted people"
);
```

## Using methods as fields

EntityGraphQL can map methods on your classes to query fields using the `GraphQLFieldAttribute`. When using `SchemaBuilder` or `SchemaType.AddAllFields()` EntityGraphQL will add any methods that have the `GraphQLFieldAttribute` as a field on that type. The parameters of the method will become the GraphQL field arguments. Mapping method parameters to GraphQL arguments follow the same rules as mutations and subscription methods.

Using `[GraphQLInputType]` on a parameter will include the parameter as an argument and use the type as an input type. `[GraphQLArguments]` will flatten the properties of that parameter type into many arguments in the schema.

When looking for a methods parameters, EntityGraphQL will

1. First all scalar / non-complex types will be added as arguments in the schema.

2. If parameter type or enum type is already in the schema it will be added at an argument.

3. Any argument or type with `GraphQLInputTypeAttribute` will be added to the schema as an `InputType`

4. Any argument or type with `GraphQLArgumentsAttribute` found will have the types properties added as schema arguments.

5. If no attributes are found it will assume they are services and not add them to the schema. _I.e. Label your parameters with the attributes or add the type to the schema beforehand._

### Note about method execution with EF

Let's look at an example.

```cs
public class DemoContext : DbContext
{
    [Description("Collection of Movies")]
    public DbSet<Movie> Movies { get; set; }
    [Description("Collection of Peoples")]
    public DbSet<Person> People { get; set; }
    [Description("Collection of Actors")]
    public DbSet<Actor> Actors { get; set; }
}

public class Movie
{
    public uint Id { get; set; }
    public string Name { get; set; }

    // other fields hidden

    public virtual DateTime Released { get; set; }
    public virtual List<Actor> Actors { get; set; }
    public virtual Person Director { get; set; }

// highlight-next-line
    [GraphQLField]
    public uint DirectorAgeAtRelease => (uint)((Released - Director.Dob).Days / 365);
}

var schema = new SchemaProvider<DemoContext>();
```

`DirectorAgeAtRelease` will become a field `directorAgeAtRelease` on the GraphQL type `Movie` with no arguments. If you query this field like

```gql
{
  movies {
    name
    directorAgeAtRelease
  }
}
```

It will generate the follow expression

```cs
(ctx) => ctx.Movies.Select(m => new {
    name = m.Name,
    directorAgeAtRelease = m.DirectorAgeAtRelease()
})
```

_If you are using EF, it does not know how to translate your method and will try to execute this method after fetching data._ This issue is the method relies on the `Director` property on `Movie` which EF doesn't know and did not fetch so you will not get the expected result. There are a few solutions to this.

#### Lazy Loading

One solution is [Microsoft.EntityFrameworkCore.Proxies](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Proxies/) to lazy load the properties, which comes with its own tradeoffs. If you install the Nuget package and follow the instructions for using lazy loading proxies these methods will work.

#### EntityFrameworkCore.Projectables Library

Another solution is [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables) (Or [Expressionify](https://github.com/ClaveConsulting/Expressionify)) which uses source generators to generate an EF compatible expression.

```cs
// configure projectables
services.AddDbContext<DemoContext>(opt =>
// highlight-next-line
    opt.UseProjectables()
);

// add the attributes to your methods
[GraphQLField]
// highlight-next-line
[Projectable]
public uint DirectorAgeAtRelease => (uint)((Released - Director.Dob).Days / 365);
```

Now the expression will be translated into expression that EF will handle. See [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables) documention for more information. Or your perferred library.

These libraries require the methods by expression - For example [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables) will not work for a method like below, _whereas lazy loading will_.

```cs
[GraphQLField]
[Projectable] // will fail as it can't generate an expression - does work with lazy loading
public uint[] AgesOfActorsAtRelease()
{
    var ages = new List<uint>();
    foreach (var actor in Actors)
    {
        ages.Add((uint)((Released - actor.Person.Dob).Days / 365));
    }
    return ages.ToArray();
}
```

## Helper methods

### WhereWhen()

The conditional statement we saw above - `!string.IsNullOrEmpty(args.firstName) ? ctx.People.Where(p => p.FirstName == args.firstName) : ctx.People` - can start to get messy when you have multiple optional arguments. It is common to have multiple arguments on fields that return a list of items to filter for different uses. To aid this EntityGraphQL has a helper method `WhereWhen()` that only applies a `Where()` method if a given statement returns `true`.

```cs
schema.Query().AddField(
    "people",
    new {
        // multiple optional arguments
        firstName = (string)null,
        lastName = (string)null
    },
    (ctx, args) => ctx.People
        .WhereWhen(p => p.FistName == args.firstName, !string.IsNullOrEmpty(args.firstName))
        .WhereWhen(p => p.LastName == args.lastName, !string.IsNullOrEmpty(args.lastName)),
    "List of people optionally filtered by a first and/or last name"
);
```

### Take(int?)

Only apply the `Take()` method if the argument has a value.

```cs
schema.Query().AddField("Field", new { limit = (int?)null }, (db, p) => db.Entity.Take(p.limit), "description");
```
