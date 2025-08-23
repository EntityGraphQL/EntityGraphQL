---
sidebar_position: 2
---

# Mutations

[GraphQLs mutations](https://graphql.org/learn/queries/#mutations) allow you to make modifications to your data.

In EntityGraphQL mutations are just .NET methods in a class called a mutation controller.

## Adding a mutation controller

Define related mutations as methods in a class, and apply the `[GraphQLMutation]` attribute to each method.

```cs
public class PeopleMutations
{
    [GraphQLMutation("Add a new person to the system")]
    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, string firstName, string lastName)
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName,
        };
        db.People.Add(person);
        db.SaveChanges();

        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }
}
```

You can add the mutation controller to a schema in the following ways:

**Register a mutation controller**

```cs
schema.AddMutationsFrom<PeopleMutations>();
```

EntityGraphQL adds the `PeopleMutations` mutation controller and all its mutation methods (those with `[GraphQLMutation]` applied) to the schema.

For each mutation request, EntityGraphQL creates a new instance of `PeopleMutations`. The constructor and mutation methods accept services passed through [dependency injection](#dependencies-injection--services).

**Register all mutation controllers implementing or deriving from a type**

```cs
schema.AddMutationsFrom<IPersonnelMutations>();
```

If the type parameter to `AddMutationsFrom` is an interface or base class, EntityGraphQL also adds as mutation controllers all types (in the same assembly) that implement the interface or derive from the base class. In example above, all classes that implement `IPersonnelMutations` would be added to the schema.

**Now we can add people!**

```graphql
mutation {
  addNewPerson(firstName: "Bill", lastName: "Murray") {
    id
    fullName
  }
}
```

Above we use our mutation to add a person and select their `fullName` and `id` in the result.

## AddMutationsFrom method arguments

```cs
  void AddMutationsFrom<TType>(SchemaBuilderOptions? options =  null) where TType : class;
```

**SchemaBuilderOptions.AutoCreateInputTypes**
If true (default = false) and an object type is encountered during reflection of the mutation parameters it will be added to the schema as an InputObject type.

When `AutoCreateInputTypes` is true, to inject a service, pass it to the constructor of the mutation controller and store it in a field. EntityGraphQL can't detect services passed as method parameters because `ServiceProvider` is not supplied at schema creation.

**SchemaBuilderOptions.AddNonAttributedMethodsInControllers**
If true (default = false), EntityGraphQL will add any method in the mutation class as a mutation without needing the `[GraphQLMutation]` attribute. Methods must be **Public** and **not inherited** but can be either **static** or **instance**.

`SchemaBuilderOptions` inherits from `SchemaBuilderOptions` and those options are passed to the `SchemaBuilder` methods. An important one for mutations is

**SchemaBuilderOptions.AutoCreateNewComplexTypes**
If true (default = true) any complex class types that a mutation returns is added to the schema as a query type if it is not already there.

## Adding a Mutations as a Delegate

You can also add individual mutation methods to the mutation type as delegates or inline methods.

```cs
class PeopleMutations
{
    public class void Configure(ISchemaProvider<DemoContext> schema)
    {
        schema.Mutation().Add("peopleMutations", "Add a new person to the system", AddNewPerson);

        schema.Mutation().Add("peopleMutations", "Do something else", (OtherArgs args) => {
            // ... mutate logic here
        });
    }

    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, string firstName, string lastName)
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName,
        };
        db.People.Add(person);
        db.SaveChanges();

        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }
}
```

## Mutation arguments

Above we have 3 arguments in our mutation. The `DemoContext` is injected using the context instance we were passed to execute the query.

The other 2 parameters EntityGraphQL does not know where they come from so will consider them arguments in the mutation field that are to be supplied.

For example the above mutation generates the follow in the GraphQL schema.

```graphql
type Mutation {
  addNewPerson(firstName: String, lastName: String): Person!
}
```

Any mutation argument that EntityGraphQL can not resolve will be added to the schema as an argument on that mutation.

### Dependencies Injection & Services

You likely want to access some services in your mutations. EntityGraphQL supports dependency injection. When you execute a query make sure you pass in an `IServiceProvider`. Here is an example with ASP.NET.

_Note: If you use [EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) the registered `IServiceProvider` is provided._

```cs
var results = _schemaProvider.ExecuteRequest(query, demoContext, HttpContext.RequestServices, null);
```

EntityGraphQL will use that `IServiceProvider` to resolve any services when calling your mutation method. All you need to do is make sure the service is registered and include it as a parameter of the mutation controller constructor or a mutation method.

```cs
// in Startup.cs
services.AddSingleton<IDemoService, DemoService>();

// your mutation method
[GraphQLMutation("Add a new person to the system.")]
public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args, IDemoService demoService)
{
    // do something cool with demoService

    return (db) => db.People.First(p => p.Id == person.Id);
}
```

Dependencies can also be defined at the class level, which is convenient when used by multiple mutations or in a helper method:

```cs
class PeopleMutations(IDemoService demoService)
{
    private readonly IDemoService demoService;

    public PeopleMutations(IDemoService demoService)
    {
        this.demoService = demoService;
    }

    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, string firstName, string lastName)
    {
        // Use ServiceHelper
    }

    public Expression<Func<DemoContext, Person>> SetPersonJob(DemoContext db, string job)
    {
        // Use ServiceHelper
    }

    void ServiceHelper(Person person)
    {
        // Use demoService
    }
}
```

Later we'll learn how to access services within query fields of the schema.

### Service vs. Schema Argument

Given the example

```cs
public Expression<Func<DemoContext, Person>> AddPerson(DemoContext db, AddPersonArgs args, string token, IDemoService demoService) {}

public class AddPersonArgs
{
  public String FirstName { get; set; }
  public String LastName { get; set; }
}
```

EntityGraphQL needs to build a schema for output and introspection. We do not know the services registered until we execute a query. Therefore EntityGraphQL provides a few options for you to help define your schema.

We know `DemoContext` is the root query context of the schema so that will not be included as an argument in the schema. Between the other 2 we need to tell EntityGraphQL.

When looking for a methods parameters, EntityGraphQL will

1. First all scalar / non-complex types will be added as arguments in the schema.

2. If parameter type or enum type is already in the schema it will be added at an argument.

3. Any argument or type with `GraphQLInputTypeAttribute` or `GraphQLArgumentsAttribute` found will be added as schema arguments.

4. If no attributes are found it will assume they are services and not add them to the schema. _I.e. Label your arguments with the attributes or add them to the schema beforehand._

These rules are also used for subscription methods and query field methods defined with `GraphQLFieldAttribute`.

#### Example: No attributes

```cs
public Expression<Func<DemoContext, Person>> AddPerson(DemoContext db, AddPersonArgs args, string token, IDemoService demoService) {}
```

Will incorrectly try to make a schema

```graphql
type Mutation {
  addPerson(args: AddPersonArgs, token: String, demoService: IDemoService)
}
```

This is not what we want and will likely fail on creation unless you also set the other types.

_Note this is fine if you have no DI services in your mutation methods._

#### Example: `GraphQLArgumentsAttribute`

Note the `s` in Arguments.

```cs
public Expression<Func<DemoContext, Person>> AddPerson(DemoContext db, [GraphQLArguments] AddPersonArgs args, string token, IDemoService demoService) {}
```

Will correctly make the below schema as we know what is in the schema and what is not.

```graphql
type Mutation {
  addPerson(
    firstName: String
    lastName: String
    token: String
  )
}
```

`GraphQLArgumentsAttribute` puts all of the properties from that class as top level arguments in the GraphQL schema.

#### Example: `GraphQLInputTypeAttribute`

```cs
public Expression<Func<DemoContext, Person>> AddPerson(DemoContext db, [GraphQLArgument] AddPersonArgs args, string token, IDemoService demoService) {}
```

Will correctly make the below schema as we know what is in the schema and what is not.

```graphql
type Mutation {
  addPerson(
    args: AddPersonArgs
    token: String
  )
}

# ...
input type AddPersonArgs {}
```

`GraphQLInputTypeAttribute` puts all that class as top level argument in the GraphQL schema. If `AutoCreateNewComplexTypes` is true (default) `AddPersonArgs` will be added as an input type. Otherwise you will need to add this yourself.

`GraphQLInputTypeAttribute` and `GraphQLArgumentsAttribute` can also be defined on the class instead of inline on the method parameters. They give you options to encapsulate arguments by grouping them and reusing them while letting you define how they appear in the schema.

Depending on the complexity of your mutation and the design of your schema will determine which method best suits you. You may end up with many method arguments to build the mutation field schema arguments. Consider a mutation that creates an object and lets you pass all the properties in.

```cs
[GraphQLMutation("Add a new person to the system.")]
public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db,
    string firstName,
    string lastName,
    string middleName,
    DateTime dob,
    int parent1Id,
    int parent2Id,
    string[] favFood,
    // ... it could go on)
{
    // ...
}
```

You may also have mutations where you want to have the same or similar arguments. EntityGraphQL lets you use a class. If a parameter in the method has the `GraphQLArgumentsAttribute` that type will be expanded. The above could be changed to the following.

```cs
[GraphQLMutation("Add a new person to the system.")]
public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args)
{
    // use args.*
}

[GraphQLArguments] // or [GraphQLArgument] if you want the AddPersonArgs as an input type
public class AddPersonArgs
{
    public String FirstName { get; set; }
    public String LastName { get; set; }
    public String MiddleName { get; set; }
    public DateTime Dob { get; set; }
    public int Parent1Id { get; set; }
    public int Parent2Id { get; set; }
    public String[] FavFood { get; set; }
    // ... it could go on
}
```

GraphQLArgument classes provide some flexibility in using inheritance etc for common mutation fields. Both still generate the same mutation field in the GraphQL schema.

```graphql
type Mutation {
  addNewPerson(
    firstName: String
    lastName: String
    middleName: String
    dob: String
    parent1Id: Int
    parent2Id: Int
    favFood: [String]
  ): Person!
}
```

## Why Use `Expression` Return Type

Note the return signature above and the result we return is an `Expression<Func<>>` that selects the person we just modified.

Just like in queries, if the mutation field returns an object type, you can ask for nested fields. This can be useful for fetching the new state of an object after an update.

One API user may ask for the `id`

```graphql
mutation {
  addNewPerson(firstName: "Bill", lastName: "Murray") {
    id
  }
}
```

And another might want more

```graphql
mutation {
  addNewPerson(firstName: "Bill", lastName: "Murray") {
    id
    firstName
    fullName
  }
}
```

As you don't know which fields an API user will request, you therefore don't know what data to load into memory and return in that object. As EntityGraphQL will execute the field selection against the returned object.

Using the `Expression<Func<>>` as a return type allows EntityGraphQL to build an expression across the whole schema graph. An example of the expression result built for the above mutation.

```cs
(DemoContext ctx) => ctx.People
    .Where(p => p.Id == <id_from_variable>)
    .Select(p => new {
        id = p.Id,
        firstName = p.FirstName,
        fullName = $"{p.FirstName} {p.LastName}"
    })
    .First()
```

This means we have access to the full schema graph from the core context of the schema and if you are using an ORM like Entity Framework it will load the requested data for you.

## Tracking Argument Values: IArgumentsTracker

EntityGraphQL provides a way to help you determine if an argument or input property was explicitly set by the user in a query or mutation, or if it is just the default .NET value. This is useful for distinguishing between "not provided" and "provided as null/default".

### Using IArgumentsTracker with Input Types

If your argument or input class implements the `IArgumentsTracker` interface (or inherits from the provided `ArgumentsTracker` base class), you can check if a property was set by the user:

```csharp
public class UpdatePersonInput : ArgumentsTracker {
    public string? Name { get; set; }
    public int? Age { get; set; }
    public string? Email { get; set; }
}

// In your mutation
public Person UpdatePerson(UpdatePersonInput input) {
    var person = GetPersonById(input.Id);

    if (input.IsSet(nameof(UpdatePersonInput.Name))) {
        person.Name = input.Name; // Update only if provided
    }
    if (input.IsSet(nameof(UpdatePersonInput.Age))) {
        person.Age = input.Age; // Update only if provided
    }
    if (input.IsSet(nameof(UpdatePersonInput.Email))) {
        person.Email = input.Email; // This could be null if explicitly set to null
    }

    SaveChanges();
    return person;
}
```

This works for both inline arguments and variables:

```graphql
# Using variables
mutation UpdatePerson($input: UpdatePersonInput) {
  updatePerson(input: $input) {
    id
    name
    age
  }
}
# Variables: { "input": { "name": "John", "email": null } }

# Using inline arguments
mutation {
  updatePerson(input: { name: "John", email: null }) {
    id
    name
    age
  }
}
```

### Using IArgumentsTracker with Simple Parameters

For simple argument lists (e.g. method parameters), you can add an `IArgumentsTracker` parameter to your mutation or query method. This allows you to check if a specific argument was set:

```csharp
public Person UpdatePersonSimple(Guid id, string? name, int? age, string? email, IArgumentsTracker argsTracker) {
    var person = GetPersonById(id);

    if (argsTracker.IsSet(nameof(name))) {
        person.Name = name;
    }
    if (argsTracker.IsSet(nameof(age))) {
        person.Age = age;
    }
    if (argsTracker.IsSet(nameof(email))) {
        person.Email = email; // Could be null if explicitly set to null
    }

    SaveChanges();
    return person;
}
```

This works seamlessly with inline arguments:

```graphql
# Only name and email are provided - age will not be updated
mutation {
  updatePersonSimple(
    id: "123e4567-e89b-12d3-a456-426614174000"
    name: "John Doe"
    email: null
  ) {
    id
    name
    age
    email
  }
}
```

## Async Mutations

EntityGraphQL supports asynchronous mutations by returning `Task<T>` from your mutation methods. This is useful for operations that call external services, perform database operations, or other async work.

```csharp
public class PeopleMutations
{
    [GraphQLMutation("Add a new person asynchronously")]
    public async Task<Expression<Func<DemoContext, Person>>> AddPersonAsync(
        DemoContext db,
        string firstName,
        string lastName,
        EmailService emailService)
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName,
        };

        db.People.Add(person);
        await db.SaveChangesAsync();

        // Send welcome email asynchronously
        await emailService.SendWelcomeEmailAsync(person);

        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }

    [GraphQLMutation("Update person with external validation")]
    public async Task<bool> UpdatePersonWithValidationAsync(
        int personId,
        string newEmail,
        ValidationService validator,
        DemoContext db)
    {
        // Validate email with external service
        var isValid = await validator.ValidateEmailAsync(newEmail);
        if (!isValid)
        {
            throw new InvalidOperationException("Email validation failed");
        }

        var person = await db.People.FindAsync(personId);
        if (person == null)
        {
            return false;
        }

        person.Email = newEmail;
        await db.SaveChangesAsync();
        return true;
    }
}
```

### Mutations vs. Async Fields

- **Mutations**: Use `[GraphQLMutation]` on methods in mutation controller classes. Return `Task<T>` for async operations. No concurrency control as each mutation field executes in order sequently as per the spec.
- **Async Fields**: Use `.ResolveAsync<Service>()` when adding fields to types. Supports concurrency control and hierarchical limiting.
