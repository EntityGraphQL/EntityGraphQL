---
sidebar_position: 1
---

# Filtering

To quickly add filtering capabilities to your collection fields use the `UseFilter()` field extension.

```cs
schema.ReplaceField("people",
    ctx => ctx.People,
    "Return a list of people. Optional filtered")
    .UseFilter();
```

If you are using the `SchemaBuilder.FromObject` you can use the `UseFilterAttribute` on your collection properties.

```cs
public class DemoContext : DbContext
{
    [UseFilter]
    public DbSet<Movie> Movies { get; set; }
    [UseFilter]
    public DbSet<Person> People { get; set; }
    [UseFilter]
    public DbSet<Actor> Actors { get; set; }
}
```

This field extension can only be used on a field that has a `Resolve` expression that is assignable to `IEnumerable` - I.e. collections. The extension adds an argument called `filter: String`.

Note: When using with the paging or sort extensions ensure you call `UseFilter` before both others. If you are using the attribute, then ensure the Filter attribute comes before the other attributes.

The `filter` argument takes a string that will be compiled to an expression and inserted into a `Where()` call. The expression is compiled against your schema and the context is the type of elements in the collection.

For example, given `ctx => ctx.People` returns a `IEnumerable<Person>` and `Person` is defined as:

```cs
public class Person
{
    public uint Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime Dob { get; set; }
    public List<Actor> ActorIn { get; set; }
    public List<Writer> WriterOf { get; set; }
    public List<Movie> DirectorOf { get; set; }
    public DateTime? Died { get; set; }
    public bool IsDeleted { get; set; }
}
```

We can write some filter expressions like so:

```graphql
{
  people(filter: "id == 12 || id == 10") {
    firstName
  }
}

{
  deletedPeople: people(filter: "isDeleted == true") {
    firstName
  }
}

{
  people(filter: "dob > \"2010-08-11T00:00:00\" && isDeleted == false") {
    firstName
  }
}
```

The expression language supports the following constants:

- Booleans - `true` & `false`
- Integers - e.g. `2`, `-8`
- Floats - e.g. `0.2`, `-8.3`
- `null`
- Strings - `"within double quotes"`; when representing a date, use an ISO 8601 format such as `"2022-07-31T20:00:00"`

The expression language supports the following operators:

- `-` - Subtraction
- `+` - Addition
- `*` - Multiply
- `/` - Divide
- `%` - Mod
- `^` - Power
- `==` - Equals
- `!=` - Not Equals
- `<=` - Less than or equal to
- `>=` - Greater than or equal to
- `<` - Less than
- `>` - Greater than
- `or` or `||` - Or
- `and` or `&&` - And

The expression language supports the following methods:

- `List.where(filter)`, or `List.filter(filter)` - Filter the list
- `List.any(filter)` - Return `true` if any of the items in the list match the filter. Otherwise `false`
- `List.first(filter?)` - Return the first item from a list. Optionally by a filter
- `List.last(filter?)` - Return the last item from a list. Optionally by a filter
- `List.take(int)` - Return the first `x` items
- `List.skip(int)` - Return the items after `x`
- `List.count(filter?)` - Return the count of a list. Optionally counting items that match a filter
- `List.orderBy(field)` - Order the list by a given field
- `List.orderByDesc(field)` - Order the list in reverse by a given field
- `string.contains(string)` - Return `true` if the specified string occurs in this string instance
- `string.startsWith(string)` - Return `true` if the beginning of this string instance matches the specified string
- `string.endsWith(string)` - Return `true` if the end of this string instance matches the specified string
- `string.toLower()` - Return the string converted to lowercase
- `string.toUpper()` - Return the string converted to uppercase


The expression language supports ternary and conditional:
- `__ ? __ : __`
- `if __ then __ else __`
