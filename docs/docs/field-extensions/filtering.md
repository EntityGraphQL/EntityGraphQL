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

The expression language supports the following methods, these are called against fields within the filter context:

- `List.any(filter)` - Return `true` if any of the items in the list match the filter. The filter within `any` is on the context of the list item type. Otherwise `false`

```gql
{
  # In C# - people.Where(p => p.ActorIn.Any(a => a.Name == "Star Wars"))
  people(filter: "actorIn.any(name == \"Star Wars\")") { ... }
}
```

- `List.count(filter?)` - Return the count of a list. Optionally counting items that match a filter

```gql
{
  # No filter - all people that acted in 3 movies
  people(filter: "actorIn.count() == 3") { ... }


  # Count only those that match the filter - all people that acted in any movie starting with "Star"
  people(filter: "actorIn.count(name.startsWith(\"Star\")) > 0") { ... }
}
```

- `List.first(filter?)` / `List.firstOrDefault(filter?)` - Return the first item from a list. Optionally by a filter
- `List.last(filter?)` / `List.lastOrDefault(filter?)` - Return the last item from a list. Optionally by a filter
- `List.take(int)` - Return the first `x` items
- `List.skip(int)` - Return the items after `x`
- `List.orderBy(field)` - Order the list by a given field
- `List.orderByDesc(field)` - Order the list in reverse by a given field
- `List.where(filter)`, or `List.filter(filter)` - Filter the list
- `string.contains(string)` - Return `true` if the specified string occurs in this string instance

```gql
{
  people(filter: "firstName.contains(\"o\")") { ... }
}
```

- `string.startsWith(string)` - Return `true` if the beginning of this string instance matches the specified string

```gql
{
  people(filter: "firstName.startsWith(\"b\")") { ... }
}
```

- `string.endsWith(string)` - Return `true` if the end of this string instance matches the specified string

```gql
{
  people(filter: "firstName.endsWith(\"b\")") { ... }
}
```

- `string.toLower()` - Return the string converted to lowercase

```gql
{
  people(filter: "firstName.toLower() == \"bob\"") { ... }
}
```

- `string.toUpper()` - Return the string converted to uppercase

```gql
{
  people(filter: "firstName.toUpper() == \"BOB\"") { ... }
}
```

- `<scalar>.isAny([])` - Return `true` if the scalar value (`string`, `int`, etc.) equals any of the values provided in the array argument of the method call.

```gql
{
  people(filter: "firstName.isAny([\"Bob\", \"Bobby\"])") { ... }
}
```

## Custom Type Converters for Filters

EntityGraphQL provides a flexible type converter system that enables runtime value conversion between types. Type converters work throughout EntityGraphQL (for mutation arguments, query variables, etc.), but are particularly useful in filter expressions when working with custom types like `Version`, `Uri`, or custom structs.

### Using Custom Types in Filters

To use a custom type in filter expressions, you need to register a type converter:

**Type Converter** (`schema.AddCustomTypeConverter`) - Enables conversion of string values to custom types. This handles:
- Runtime conversion (query variables, mutation arguments)
- Compile-time conversion (string literals in binary comparisons like `version >= "1.2.3"`)
- Array conversions (`isAny` arrays)

Type converters work throughout EntityGraphQL, not just in filters.

### Example: Filtering by Version

```cs
public class Product
{
    public string Name { get; set; }
    public Version Version { get; set; }
}

var schema = SchemaBuilder.FromObject<ProductContext>();

// Add type converter - handles both runtime and compile-time conversion
schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));

// Mark the products field as filterable
schema.ReplaceField("products", ctx => ctx.Products, "List of products")
    .UseFilter();
```

Now you can use both binary comparisons and `isAny` with Version in filters:

```graphql
{
  # Binary comparison with string literal
  products(filter: "version >= \"1.2.0\"") {
    name
    version
  }

  # Using isAny with Version
  products(filter: "version.isAny([\"1.2.3\", \"2.0.0\"])") {
    name
    version
  }

  # Combining both
  products(filter: "version >= \"1.2.0\" && version.isAny([\"1.2.3\", \"2.0.0\"])") {
    name
    version
  }
}
```

### Converter Patterns

EntityGraphQL supports three registration approaches:

**From-To Converter** - Maps a specific source type to a target type:

```cs
schema.AddCustomTypeConverter<string, Version>((s, _) => Version.Parse(s));
```

**To-Only Converter** - Converts any source to a particular target type:

```cs
schema.AddCustomTypeConverter<Uri>(
    (obj, _) => obj switch
    {
        string s => new Uri(s, UriKind.RelativeOrAbsolute),
        Uri u => u,
        _ => new Uri(obj!.ToString()!, UriKind.RelativeOrAbsolute),
    }
);
```

**From-Only Converter** - Converts a source type to multiple possible targets:

```cs
schema.AddCustomTypeConverter<string>(
    (s, toType, _) =>
    {
        if (toType == typeof(Uri))
            return new Uri(s, UriKind.RelativeOrAbsolute);
        if (toType == typeof(Version))
            return Version.Parse(s);
        return s;
    }
);
```

### Enum Converters

Custom converters work great with enum types in filters:

```cs
public enum Status { Active, Inactive, Pending }

schema.AddCustomTypeConverter<string, Status>(
    (s, _) =>
    {
        if (Enum.TryParse<Status>(s, ignoreCase: true, out var val))
            return val;
        throw new ArgumentException($"Invalid enum value '{s}'");
    }
);

// Use in filter
{
  people(filter: "status.isAny([\"Active\", \"Pending\"])") { ... }
}
```

### Using with GraphQL Variables

Type converters automatically work with GraphQL variables:

```graphql
query GetProductsByVersions($versions: [String!]!) {
  products(filter: "version.isAny($versions)") {
    name
    version
  }
}
```

```json
{
  "versions": ["1.2.3", "2.0.0"]
}
```

The expression language supports ternary and conditional:

- `__ ? __ : __`
- `if __ then __ else __`

## GraphQL Variables in Filters

The filter extension supports GraphQL variables using the `$variableName` syntax. This allows you to parameterize your filter expressions, making them more dynamic and reusable.

### Example Variable Usage

You can use multiple variables in a single filter expression:

```graphql
query GetPeopleByRange($minAge: Int!, $status: String!) {
  people(filter: "age >= $minAge && status == $status") {
    firstName
    lastName
    age
  }
}
```

With variables:

```json
{
  "minAge": 18,
  "status": "active"
}
```

## Service Fields in Filters

When using the filter extension with fields that resolve data from services (using `Resolve<TService>()`) and have two-pass execution enabled (`ExecuteServiceFieldsSeparately = true`, which is the default), EntityGraphQL automatically handles filter splitting to optimize query performance.

### How Filter Splitting Works

The filter extension uses a `FilterSplitter` that automatically separates filter expressions into two parts:

1. **Database-safe filters**: Expressions that only reference database fields, executed directly against the database (e.g., Entity Framework)
2. **Service-dependent filters**: Expressions that reference service fields, executed in-memory after the service data is resolved

This ensures that:

- Entity Framework can optimize database queries with only the database-safe portion of the filter
- Service fields work correctly in filters without causing EF translation errors
- Performance is optimized by filtering as much as possible at the database level

### Example with Service Fields

Given a `Person` type with a service field:

```cs
schema.UpdateType<Person>(type => {
    type.AddField("age", "Person's calculated age")
        .Resolve<IAgeService>((person, ager) => ager.GetAge(person.Birthday));
});
```

You can use filters that mix database and service fields:

```graphql
{
  # Filter combining database field (name) and service field (age)
  people(filter: "name.startsWith('John') && age > 21") {
    name
    age
  }
}
```

EntityGraphQL will automatically split this into:

1. **Database filter**: `name.startsWith('John')` - executed against the database / main context
2. **Service filter**: `age > 21` - executed in-memory after age calculation

### Filter Splitting Rules

The filter splitter follows these rules for optimal performance:

- **AND expressions**: Split into separate database and service parts when possible
- **OR expressions**: Moved entirely to service execution if they contain any service fields (cannot be safely split)
- **NOT expressions**: Handled appropriately based on whether they contain service fields

:::info Performance Note

Filter splitting is automatically enabled when `ExecuteServiceFieldsSeparately = true` (the default). This provides optimal performance by leveraging database query optimization while supporting service fields in filters.

If you disable two-pass execution (`ExecuteServiceFieldsSeparately = false`), all filters will execute in-memory, which may impact performance for large datasets if your query context is a database context.

:::
