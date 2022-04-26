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

Now we can add it to the schema.

```
schema.AddMutationFrom(new PeopleMutations());
```

Now we can add people!

```
mutation {
    addNewPerson(firstName: "Bill", lastName: "Murray") {
        id
        fullName
    }
}
```

Above we use our mutation to add a person and select their `fullName` and `id` in the result.

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

EntityGraphQL will use that `IServiceProvider` to resolve any services when calling your mutation method. All you need to do is make sure the service is registered and include it in the method signature of the mutation.

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

Later we'll learn how to access services within fields of the query schema.

# Validation

You can use the `[Required]`, `[Range]` & `[StringLength]` attributes to add validaiton to your mutation arguments. Example

```
public class ActorArgs
{
  [Required(AllowEmptyStrings = false, ErrorMessage = "Actor Name is required")]
  public string Name { get; set; }
  [Range(0, 900, ErrorMessage = "Age must be positive and less than 900")]
  public int Age { get; set; }
  [StringLength(200, ErrorMessage = "Description must be less than 200 characters")]
  public string Description { get; set; }
}
```

If any of those validations fail, the graph QL result will have errors for each one that failed. If your model validation fails your mutation method _will not be called_.

Throwing an exception in your mutation will cause the the error to be reported in the GraphQL response. You can also collect multiple error messages instead of throwing an exception on the first error using the `GraphQLValidator` service.

```
public class MovieMutations
{
  [GraphQLMutation]
  public Expression<Func<MyDbContext, Movie>> AddActor(MyDbContext db, ActorArgs args, GraphQLValidator validator)
  {
    if (string.IsNullOrEmpty(args.Name))
      validator.AddError("Name argument is required");
    if (args.age <= 0)
      validator.AddError("Age argument must be positive");

    if (validator.HasErrors)
      return null;

    // do your magic here. e.g. with EF or other business logic
    var movie = db.Movies.First(m => m.Id == args.Id);
    var actor = new Person { Name = args.Name, ... };
    movie.Actors.Add(actor);
    db.SaveChanges();
    return ctx => ctx.Movies.First(m => m.Id == movie.Id);
  }
}
```

Of course if you opt to throw an exception it will be caught and included in the error results.
