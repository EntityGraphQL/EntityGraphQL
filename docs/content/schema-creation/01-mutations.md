---
title: 'Mutations'
metaTitle: 'Adding mutations to your schema - EntityGraphQL'
metaDescription: 'Add mutations to your GraphQL schema'
---

Lets add some mutations. Mutations are GraphQLs way all allowing modifications to data.

Read more about GraphQL mutations [here](https://graphql.org/learn/queries/#mutations).

In EntityGraphQL mutations are just .NET methods with the `[GraphQLMutation]` attribute that receive arguments, perform work (update data etc.) and return data. You can contain these in a mutation class where related mutations can be located near each other.

# Adding a Mutation

```
public class PeopleMutations
{
    [GraphQLMutation("Add a new person to the system)]
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

You can use the `System.ComponentModel.DataAnnotations.Required` attribute to add validaiton to your mutation arguments. Example

```
public class ActorArgs
{
  [Required(AllowEmptyStrings = false, ErrorMessage = "Actor Name is required")]
  public string Name { get; set; }
  public int Age { get; set; }
}
```

If you do not supply a non empty string as the `name` argument for the mutation you'll get an error message.

If your model validation fails from a `RequiredAttribute` you mutation method _will not be called_.

You can also add multiple error messages instead of throwing an exception on the first error using the `GraphQLValidator` service.

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
