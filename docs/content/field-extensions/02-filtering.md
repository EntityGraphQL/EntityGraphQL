---
title: "Filtering"
metaTitle: "Add filtering to your fields - EntityGraphQL"
metaDescription: "Add filtering to your fields"
---

To quickly add filtering capabilities to your collection fields use the `UseFilter()` field extension.

```
schema.ReplaceField("people",
    ctx => ctx.People,
    "Return a list of people. Optional filtered")
    .UseFilter();
```

This field extension can only be used on a field that has a `Resolve` expression that is assignable to `IEnumerable` - I.e. collections. The extension adds an argument called `filter: String`.

The `filter` argument takes a string that will be compiled to an expression and inserted into a `Where()` call. The expression is compiled against your schema and the context is the type of elements in the collection.

For example, given `ctx => ctx.People` returns a `IEnumerable<Person>` and `Person` is defined as:

```
public class Person
{
    public uint Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime Dob { get; set; }v
    public List<Actor> ActorIn { get; set; }
    public List<Writer> WriterOf { get; set; }
    public List<Movie> DirectorOf { get; set; }
    public DateTime? Died { get; set; }
    public bool IsDeleted { get; set; }
}
```

We can write some filter expressions like so:

```
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