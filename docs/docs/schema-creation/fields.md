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

## Turning methods into fields

EntityGraphQL can map methods on your classes to query fields using the `GraphQLFieldAttribute`. When using `SchemaBuilder` or `SchemaType.AddAllFields()` EntityGraphQL will add any methods that have the `GraphQLFieldAttribute` on them as a field on that type. The parameters of the method will become the GraphQL field arguments. Mapping method parameters to GraphQL arguments follow the same rules as mutations and subscription methods.

Using `[GraphQLInputType]` on a parameter will include the parameter as an argument and use the type as an input type. `[GraphQLArguments]` will flatten the properties of that parameter type into many arguments in the schema.

When looking for a methods parameters, EntityGraphQL will

1. First all scalar / non-complex types will be added as arguments in the schema.

2. If parameter type or enum type is already in the schema it will be added at an argument.

3. Any argument or type with `GraphQLInputTypeAttribute` will be added to the schema as an `InputType`

4. Any argument or type with `GraphQLArgumentsAttribute` found will have the types properties added as schema arguments.

5. If no attributes are found it will assume they are services and not add them to the schema. _I.e. Label your arguments with the attributes or add them to the schema beforehand._

### Note about execution with EF

Let's look at an example.

```cs
public class MyContext : DbContext
{
    public DbSet<Task> Tasks { get; set; }
}

public class Task
{
    public uint Id { get; set; }
    public string Title { get; set; }
    public DateTime Due { get; set; }

    public int DaysUntilDue() => (DateTime.Now - Due).TotalDays;
}

var schema = new SchemaProvider<MyContext>();
```

`DaysUntilDue` will become a field `daysUntilDue` on the GraphQL type `Task` with no arguments. If you query this field like

```gql
{
  tasks {
    id
    daysUntilDue
  }
}
```

It will generate the follow expression

```cs
(ctx) => ctx.Task.Select(t => new {
    id = t.Id,
    daysUntilDue = t.DaysUntilDue()
})
```

_Depending on the complexitiy of your method, if you are using EF, it may fail to execute as EF doesn't know what to do with the method._

If your method includes a service as a parameter this will be handled by EntityGraphQL and executed after fetching data from EF. See [Entity Framework](../entity-framework) section for more information.

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
