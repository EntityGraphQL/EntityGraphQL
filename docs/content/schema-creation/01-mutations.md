---
title: 'Mutations'
metaTitle: 'Adding mutations to your schema - EntityGraphQL'
metaDescription: 'Add mutations to your GraphQL schema'
---

Mutations are GraphQLs way of allowing you to make modifications to your data.

Read more about GraphQL mutations [here](https://graphql.org/learn/queries/#mutations).

In EntityGraphQL mutations are just .NET methods and there are a few ways to add or define them.

# Adding Mutations from a class

You can keep related mutations in a class and marked each mutation method with the `[GraphQLMutation]` attribute and use the `schema.AddMutationsFrom()`.

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

Now we can add it to the schema in the following ways:


**Providing an Instance of the mutation class**
```
schema.AddMutationsFrom(new PeopleMutations());
```

EntityGraphQL will find all methods marked as [GraphQLMutation] on the PeopleMuations type and add them as mutations.  

When calling the mutuation it will use the instance of the PeopleMuations you provided.

**As a class generic argument**
```
schema.AddMutationsFrom<PeopleMutations>();
```

EntityGraphQL will find all methods marked as [GraphQLMutation] on the PeopleMuations type and add them as mutations.  

When calling the mutuation it will ask the ServiceProvider for an instance of the class allowing for dependency injection at the constructor level, if that fails to return a result it will use Activator.CreateInstance.

**As an interface/base class generic argument**

EntityGraphQL actually looks for all types (in the same assembly) that implement the interface or base class, meaning you could mark all your mutation classes with a marker interface like IMutationClass and they will all be registered with one line

```
schema.AddMutationsFrom<IMutationClass>);
```

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

**autoAddInputTypes**
If true, any class types seen in the mutation argument properties will be added to the schema

**addNonAttributedMethods**
If true, EntityGraphQL will add any method in the mutation class as a mutation without needing the [GraphQLMutation] attribute.  Methods must be **Public** and **not inherited** but can be either **static** or **instance**.


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

_Note if you use [EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) the registered `IServiceProvider` is provided._

```
var results = _schemaProvider.ExecuteRequest(query, demoContext, HttpContext.RequestServices, null);
```

EntityGraphQL will use that `IServiceProvider` to resolve any services when calling your mutation method. All you need to do is make sure the service is registered and include it in the method signature of the mutation.  This includes both constructor arguments (when a mutation class instance is not provided) and method arguments for the specific mutation.

```
// in Startup.cs
services.AddSingleton<IDemoService, DemoService>();

// your mutation method
[GraphQLMutation("Add a new person to the system)]
public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args, IDemoService demoService)
{
    // do something cool with demoService

    return (ctx) => ctx.People.First(p => p.Id == person.Id);
}
```

Later we'll learn how to access services within query fields of the schema.
