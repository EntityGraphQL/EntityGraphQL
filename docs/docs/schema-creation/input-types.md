---
sidebar_position: 5
---

# Input Types

We've seen passing scalar values, like enums, numbers or strings, as arguments into a field. [Input types](https://graphql.org/learn/schema/#input-types) allow us to define complex types that can be used as an argument. This is particularly valuable in the case of mutations, where you might want to pass in a whole object to be created.

Input types differ to regular Object types largely because they can't have arguments on their fields. There are just a data object. As described in the spec:

> The fields on an input object type can themselves refer to input object types, but you can't mix input and output types in your schema. Input object types also can't have arguments on their fields.

```cs
public class PeopleMutations
{
    [GraphQLMutation("Add a new person to the system")]
    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args)
    {
        var person = new Person
        {
            FirstName = args.PersonInput.FirstName,
            LastName = args.PersonInput.LastName,
        };
        db.People.Add(person);
        db.SaveChanges();

        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }
}

[MutationArguments]
public class AddPersonArgs
{
    public PersonInput PersonInput { get; set; }
}

public class PersonInput
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}


// creating our schema
schema.AddInputType<PersonInput>("PersonInput", "New person data")
    .AddAllFields();
```

You could of course use the `Person` class from your data model directly and be selective about the fields you add to the GraphQL schema.

```cs
public class PeopleMutations
{
    [GraphQLMutation("Add a new person to the system")]
    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args)
    {
        db.People.Add(args.PersonInput);
        db.SaveChanges();
        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }
}

[MutationArguments]
public class AddPersonArgs
{
    public Person PersonInput { get; set; }
}

// creating our schema
var type = schema.AddInputType<Person>("PersonInput", "New person data")
type.AddField("firstName", p => p.FirstName, "First name);
type.AddField("lastName", p => p.LastName, "Last name);
```

You can also use complex types in field arguments.

```cs
public class FilterInput
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

schema.Query().AddField(
    "people",
    new {
        filter = (FilterInput)null
    },
    (ctx, args) => ctx.People
        .WhereWhen(p => p.FistName == args.filter.firstName, !string.IsNullOrEmpty(args.filter.firstName))
        .WhereWhen(p => p.LastName == args.filter.lastName, !string.IsNullOrEmpty(args.filter.lastName)),
    "List of people optionally filtered by a first and/or last name"
);

schema.AddInputType<FilterInput>("FilterInput", "Filter data for people")
    .AddAllFields();
```

The larger impact of these choices can be seen in the resulting schema and use of the API.

With scalar arguments.

```json
POST localhost:5000/graphql
    {
    "query": "mutation AddPerson($firstName: String!, $lastName: String!) {
        addNewPerson(firstName: $firstName, lastName: $lastName) { id }
    }",
    "variables": {
        "firstName": "Bill",
        "lastName": "Murray"
    }
}
```

With an input type argument

```json
POST localhost:5000/graphql
{
    "query": "mutation AddPerson($person: PersonInput!) {
        addNewPerson(personInput: $person) { id }
    }",
    "variables": {
        "personInput": {
            "firstName": "Bill",
            "lastName": "Murray"
        }
    }
}
```

Input types can be modified using the [OneOf](../directives/schema-directives) Schema Directive to tell clients that only one field should contain a value.