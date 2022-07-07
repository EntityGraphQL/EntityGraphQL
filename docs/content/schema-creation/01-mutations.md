---
title: 'Mutations'
metaTitle: 'Adding mutations to your schema - EntityGraphQL'
metaDescription: 'Add mutations to your GraphQL schema'
---

[GraphQLs mutations](https://graphql.org/learn/queries/#mutations) allow you to make modifications to your data.

In EntityGraphQL mutations are just .NET methods in a class called a mutation controller.

# Adding a mutation controller

Define related mutations as methods in a class, and apply the `[GraphQLMutation]` attribute to each method.

```
public class PeopleMutations
{
    [GraphQLMutation("Add a new person to the system")]
    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args)
    {
        var person = new Person
        {
            FirstName = args.FirstName,
            LastName = args.LastName,
        };
        db.People.Add(person);
        db.SaveChanges();

        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }
}

[MutationArguments]
public class AddPersonArgs
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
```

You can add the mutation controller to a schema in the following ways:

**Register a mutation controller**

```
schema.AddMutationsFrom<PeopleMutations>();
```

EntityGraphQL adds the `PeopleMutations` mutation controller and all its mutation methods (those with [GraphQLMutation] applied).

For each mutation request, EntityGraphQL creates a new instance of `PeopleMuations` and provides services to it through [dependency injection](#dependenciesinjection&services).

**Register all mutation controllers implementing or derving from a type**

```
schema.AddMutationsFrom<IPersonnelMutations>();
```

If the type parameter to `AddMutationsFrom` is an interface or base class, EntityGraphQL also adds as mutation controllers all types (in the same assembly) that implement the interface or derive from the base class. In example above, all classes that implement `IPersonnelMutations` would be added to the schema.

**Providing an Instance of the mutation class**

```
schema.AddMutationsFrom(new PeopleMutations());
```

EntityGraphQL will find all methods marked as [GraphQLMutation] on the PeopleMuations type and add them as mutations.

When calling the mutation it will use the instance of the PeopleMuations you provided.

This method is considered obsolete and will be removed in a future version. We suggest you use one of the above methods and utilse the ServiceProvider to register your mutation classes with your desired lifetime.

**Now we can add people!**

```
mutation {
    addNewPerson(firstName: "Bill", lastName: "Murray") {
        id
        fullName
    }
}
```

Above we use our mutation to add a person and select their `fullName` and `id` in the result.

# AddMutationsFrom method arguments

```
  void AddMutationsFrom<TType>(TType? mutationClassInstance =  null, bool autoAddInputTypes = false, bool addNonAttributedMethods = false) where TType : class;
```

**mutationClassInstance**
Instance of the mutation class, if not provided then EntityGraphQL will try obtain one from the ServiceProvider or fallback to Activator.CreateInstance

_Obsolete_

**autoAddInputTypes**
If true, any class types seen in the mutation argument properties will be added to the schema

**addNonAttributedMethods**
If true, EntityGraphQL will add any method in the mutation class as a mutation without needing the [GraphQLMutation] attribute. Methods must be **Public** and **not inherited** but can be either **static** or **instance**.

# Adding a Mutations as a Delegate

You can also add individual mutation methods to the mutation type as delegates or inline methods.

```
public class PeopleMutations
{
    public class void Configure(ISchemaProvider<DemoContext> schema)
    {
        schema.Mutation().Add("peopleMutations", "Add a new person to the system", AddNewPerson);

        schema.Mutation().Add("peopleMutations", "Do somethign else", (OtherArgs args) => {
            // ... mutate logic here
        });
    }

    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args)
    {
        var person = new Person
        {
            FirstName = args.FirstName,
            LastName = args.LastName,
        };
        db.People.Add(person);
        db.SaveChanges();

        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }
}
```

# Why Use `Expression` Return Type

Note the return signature above and the result we return is an `Expression<Func<>>` that selects the person we just modified.

Just like in queries, if the mutation field returns an object type, you can ask for nested fields. This can be useful for fetching the new state of an object after an update.

One API user may ask for the `id`

```
mutation {
    addNewPerson(firstName: "Bill", lastName: "Murray") {
        id
    }
}
```

And another might want more

```
mutation {
    addNewPerson(firstName: "Bill", lastName: "Murray") {
        id firstName fullName
    }
}
```

As you don't know which fields an API user will request, you therefore don't know what data to load into memory and return in that object. As EntityGraphQL will execute the field selection against the returned object.

Using the `Expression<Func<>>` as a return type allows EntityGraphQL to build an expression across the whole schema graph. An example of the expression result built for the above mutation.

```
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

# Dependencies Injection & Services

You likely want to access some services in your mutations. EntityGraphQL supports dependency injection. When you execute a query make sure you pass in an `IServiceProvider`. Here is an example with ASP.NET.

_Note: If you use [EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) the registered `IServiceProvider` is provided._

```
var results = _schemaProvider.ExecuteRequest(query, demoContext, HttpContext.RequestServices, null);
```

EntityGraphQL will use that `IServiceProvider` to resolve any services when calling your mutation method. All you need to do is make sure the service is registered and include it as a parameter of the mutation controller constructor or a mutation method.

```
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

Later we'll learn how to access services within query fields of the schema.
