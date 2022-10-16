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

## [GraphQLField] attribute
SchemaBuilder.FromObject will automatically add another methods on the context or types marked with [GraphQLField] as fields.

Example:
```cs
 public class ContextFieldWithMethod {
    public IEnumerable<TypeWithMethod> Fields { get; set; }

    [GraphQLField]
    public int TestMethod()
    {
        return 23;
    }
}
public class TypeWithMethod
{
    [GraphQLField]
    public int MethodField()
    {
        return 1;
    }

    [GraphQLField]
    public int MethodFieldWithArgs(int value)
    {
        return value;
    }

    [GraphQLField]
    public int MethodFieldWithTwoArgs(int value, int value2)
    {
        return value + value2;
    }

    [GraphQLField]
    public int MethodFieldWithDefaultArgs(int value = 27)
    {
        return value;
    }
}
```

Can then be queried as following:

```
 query TypeWithMethod {
    testMethod
    fields {
        methodField
        methodFieldWithArgs(value: $value)
        methodFieldWithTwoArgs(value: $value, value2: $value2)
        methodFieldWithDefaultArgs
    }
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
