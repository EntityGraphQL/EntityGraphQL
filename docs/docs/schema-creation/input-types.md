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

[GraphQLArguments]
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

[GraphQLArguments]
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

## Tracking Input Field Values

EntityGraphQL provides `IArgumentsTracker` functionality to help you determine if an input field was explicitly provided by the user. This is especially useful for partial updates where you want to distinguish between "not provided" and "provided as null/default".

### Using ArgumentsTracker with Input Types

Make your input type inherit from `ArgumentsTracker` to track which fields were set:

```csharp
public class UpdatePersonInput : ArgumentsTracker
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public int? Age { get; set; }
}

// Register as input type
schema.AddInputType<UpdatePersonInput>("UpdatePersonInput", "Person update data")
    .AddAllFields();

// Use in mutation
[GraphQLMutation("Update a person's information")]
public Expression<Func<DemoContext, Person>> UpdatePerson(DemoContext db, int id, UpdatePersonInput input)
{
    var person = db.People.Find(id);
    
    // Only update fields that were explicitly provided
    if (input.IsSet(nameof(UpdatePersonInput.FirstName)))
        person.FirstName = input.FirstName;
        
    if (input.IsSet(nameof(UpdatePersonInput.LastName)))
        person.LastName = input.LastName;
        
    if (input.IsSet(nameof(UpdatePersonInput.Email)))
        person.Email = input.Email; // Could be null if explicitly set to null
        
    if (input.IsSet(nameof(UpdatePersonInput.Age)))
        person.Age = input.Age;
    
    db.SaveChanges();
    return ctx => ctx.People.First(p => p.Id == id);
}
```

### Using with Inline Arguments and Variables

The tracking works consistently with both approaches:

```graphql
# Using variables - only firstName and email are updated
mutation UpdatePersonVar($input: UpdatePersonInput!) {
    updatePerson(id: 1, input: $input) { id firstName lastName email }
}
# Variables: { "input": { "firstName": "John", "email": null } }

# Using inline arguments - only firstName and email are updated  
mutation {
    updatePerson(id: 1, input: { firstName: "John", email: null }) {
        id firstName lastName email
    }
}
```

In both cases:
- `input.IsSet("FirstName")` returns `true`
- `input.IsSet("Email")` returns `true` 
- `input.IsSet("LastName")` returns `false`
- `input.IsSet("Age")` returns `false`

### Nested Input Types

Tracking also works with nested input types:

```csharp
public class PersonAddressInput : ArgumentsTracker
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
}

public class UpdatePersonWithAddressInput : ArgumentsTracker
{
    public string? FirstName { get; set; }
    public PersonAddressInput? Address { get; set; }
}

// Usage in mutation
if (input.IsSet(nameof(UpdatePersonWithAddressInput.Address)) && input.Address != null)
{
    if (input.Address.IsSet(nameof(PersonAddressInput.Street)))
        person.Address.Street = input.Address.Street;
        
    if (input.Address.IsSet(nameof(PersonAddressInput.City)))
        person.Address.City = input.Address.City;
}
```

```graphql
mutation {
    updatePersonWithAddress(id: 1, input: {
        firstName: "John",
        address: {
            city: "New York"
        }
    }) {
        id firstName address { street city postalCode }
    }
}
```

This allows for very granular control over which nested fields are updated.
