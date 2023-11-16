---
sidebar_position: 10
---

# Adding Other Data Sources or Services

EntityGraphQL lets you add fields that resolve (fetch) data from sources other than the core query context you created your schema with. This is powerful as it let's you create a single API that brings together multiple data sources into an object graph.

## `Resolve<TService>()` Fields

To use other services in a field we use the `Resolv<TService>()` method on the field. EntityGraphQL uses the `IServiceProvider` you pass on execution to resolve the services.

Let's use a service in our `Person` type example.

```cs
// Where you create your schema
schema.UpdateType<Person>(personType => {
    personType.AddField("age", "Person's age")
        .Resolve<IAgeService>((person, srv) => srv.GetAge(person.Dob));
});

// Startup.cs
services.AddSingleton<IAgeService, AgeService>();

// AgeService.cs
public class AgeService
{
    public int GetAge(DateTime dob)
    {
        return (now - dob).TotalYears;
    }
}
```

Now when someone requests the `age` field on a person the result will be resolved by executing the service `srv.GetAge(person.Dob)`. Of course you could calculate the age directly in the field's resolve expression without a service, this is just a demonstration on how to use other services.

## Services With Non-Scalar Types

A service can return any type. If it is a complex type you will need to add it to the schema.

```cs
schema.Query().AddField("users", "Get list of users")
    .Resolve<IUserService>(
        // ctx is the core context we created the schema with. For this field we don't use it
        (ctx, srv) => srv.GetUsers()
    );

schema.AddType<User>("User", "User information")
    .AddAllFields();

public class UserService : IUserService
{
    public List<User> GetUsers() { ... }
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
}
```

## Connecting Back to the Core Context

With the User example above you might want to add fields to the `User` type that brings in data back from the core context. This let's you create a rich object graph for querying.

When joining non-core context types back to the core context you need to use `Resolve()` again.

```cs
schema.UpdateType<User>(userType => {
    userType.AddField("tasks", "List of projects assigned to the user")
        .Resolve<DemoContext>(
            (user, db) => db.Projects.Where(project => project.AssignedToId == user.Id)
        );
});
```

Now we get query the user and their projects at the same time.

```graphql
query {
  users {
    name
    projects {
      name
      summary
    }
  }
}
```

## Complex Service Types

If the type you want to define in the schema has data resolved from multiple different methods in your service you can create a service type class.

Let's say we want a root-level `metrics` field where each sub-field uses a service to load the value. If someone only queries 1 of the fields you don't want all of them to resolve/execute. To achieve this we can do the following.

Define a `Metrics` class that uses a service to provide the functionality.

```cs
public class Metrics
{
    private IMetricService m;
    public Metrics(IMetricService m)
    {
        this.m = m;
    }
    public int TotalWebhooks => m.TotalWebhooks();
    public int TotalApiKeys => m.TotalApiKeys();
    public int TotalUsers => m.TotalUsers();
}
```

No we can add the types & fields to GraphQL.

```cs
// add the type
var metricsType = adminSchema.AddType<Metrics>("Metrics", "Contains summary metrics")
    .AddAllFields();

// add the root-level field
adminSchema.Query().AddField("metrics", "Return summary metrics")
    .Resolve<IMetricService>(
        (db, m) => new Metrics(m)
    );
}
```

Now we can query

```graphql
{
  metrics {
    totalWebhooks
  }
}
```

To demonstrate that only the `m.TotalWebhooks()` method is called, here is what is produced as the .NET expression.

```cs
(MyDbContext db, IMetricService m) => {
    var context = new Metric(m);
    return new
    {
        totalWebhooks = context.TotalWebhooks,
    }
};
```

You'll see none of the other fields are in the expression and therefore the methods will not execute.

## Bulk data loading

You'll notice that a service may be used to resolve a field (that field may return a complex object), what happens if that field in on a type that is in a list result? For example let's assume our user data comes from an external service

```cs
schema.UpdateType<Project>(type => {
    type.AddField("createdBy", "Get the user details of user that created this project")
    .Resolve<IUserService>((project, srv) => srv.GetUserById(project.CreatedById));
});
```

If we query all projects

```gql
{
  projects {
    name id
    createdBy { name }
  }
}
```

EntityGraphQL will create an expression similar to this.

```cs
(queryContext, userService) => queryContext.Projects.Select(project => {
    name = project.Name,
    id = project.Id,
    createdBy = new {
        name = userService.GetUserById(project.CreatedById).Name
    }
});
```

Note this is just to demonstrate concepts, EntityGraphQL will wrap the call to `userService.GetUserById(project.Id)` to avoid multiple calls if you select other fields on the `createdBy` field. However it will be called for each `Project` result. If there are 1,000 projects, `GetUserById` will be called 1,000 times with the ID for that project.

Depending on what `GetUserById` does this may not be an issue. You can also build your services to include a short-timed cache for results to speed things up. However, you may want to actually load all the user data for all projects at once. To do this you can use `ResolveBulk`.

```cs
schema.UpdateType<Project>(type =>
{
    type.AddField("createdBy", "Get the user details of user that created this project")
      // normal service to fetch the User object for creator of the Project type
      .Resolve<UserService>((proj, users) => users.GetUserById(proj.CreatedById))
      // Bulk service used to fetch many User objects
      .ResolveBulk<UserService, int, User>(proj => proj.CreatedById, (ids, srv) => srv.GetAllUsers(ids));
});
```

Now the following quereis will trigger one or the other service call

```gql
{
  # ResolveBulk
  projects {
    # project fields
    name id
    # service field - resolved with ResolveBulk expression for all Projects loaded
    createdBy { name }
  }

  # Resolve
  project(id: 78) {
    # project fields
    name id
    # service field - resolved with Resolv service expression for the single project loaded
    createdBy { name }
  }
}
```

Instead of calling `users.GetUserById()` for each project to resolve `createdBy { name }`, EntityGraphQL will build a list of keys using the `proj => proj.CreatedById` expression from the list of projects and then call the `(ids, srv) => srv.GetAllUsers(ids)` expression once for the whole list of projects in the results.

The bulk loader method signature needs to match the following

```cs
public IDictionary<TKey, TResult> MethodName(IEnumerable<TKey> data) {}

// Example of this above
public IDictionary<int, User> GetAllUsers(IEnumerable<int> data) {}
```

The method returneds a `IDictionary<>` where the key is the same type as the key selector in `ResolveBulk` (`proj => proj.CreatedById` above) and the value is the return type of the field (`User` in the above example).

`ResolveBulk` signature is `ResolveBulk<TService, TKey, TResult>(ctx => keySelector, (ids, service) => dataLoader)` where `TService` is the service that will be injected and used to load the data. `keySelector` is an expression that is used to select the IDs/keys from each project object. `dataLoader` is an expression that will be passed a list of those selected IDs/keys and should return an `IDictionary<TKey, TResult>` that maps each result to a key.

It might help to conceptionally see what this is doing in code.

```cs
// before we bulk load service data
var data = ctx.Projects.Select(p => new {
  name = p.Name,
  id = p.Id
  p.CreatedById
});

// We now select the keys using the keySelector from ResolveBulk
var projectUserKeys = data.Projects.SelectMany(p => p.CreatedById);

// call service bulk loader
var projectUsers = userService.GetAllUsers(projectUserKeys);

// Now we can select out the query fields
var result = data.Select(p => new {
  p.name,
  p.id
  user = new {
    name = projectUsers[p.CreatedById].Name,
    // ... any other fields
  }
});
```

## Limitation using services with `[GraphQLField]` method fields

Because EntityGraphQL handles service fields by executing an expression without those fields and _rewriting_ the expressions to work with the resulting type - see [How EntityGraphQL handles services](../library-compatibility/entity-framework) section for more details - you cannot use services with a method as EntityGraphQL cannot rewrite and data may be missing.

```cs
public class Movie
{
    public int Id { get; set; }
    // ...

    [GraphQLField]
    public uint[] AgesOfActorsAtRelease(IAgeCalculator calc)
    {
        List<uint> ages = calc.CalculateAges(Released, Actors);
        return ages
    }
}

public class AgeCalulator : IAgeCalculator
{
    public List<uint> CalculateAges(DateTime released, IEnumerable<Actor>)
    {
        var ages = new List<uint>();
        foreach (var actor in actors)
        {
            ages.Add((uint)((released - actor.Person.Dob).Days / 365));
        }
        return ages.ToArray();
    }
}
```

With a query:

```graphql
{
  movies {
    title
    released
    agesOfActorsAtRelease
  }
}
```

In the above example `AgesOfActorsAtRelease` is a method on type `Movie`. [When EntityGraphQL executes service fields](../library-compatibility/entity-framework) it first builds and executes executes the following (which is safe for EF).

```cs
(DemoContext ctx) = > ctx.Movies.Select(m => new {
    title = m.Title,
    released = m.Released
}).ToList();
```

The resulting type is no longer `Movie` and we can no longer find the `AgesOfActorsAtRelease` method. Please use the API on `Field` as above:

```cs
schema.UpdateType<Movie>(type => {
    type.AddField("agesOfActorsAtRelease", "All the actors ages")
        .Resolve<IAgeCalculator>((movie, srv) => srv.CalculateAges(movie.Released, movie.Actors));
});
```

As `(movie, srv) => srv.CalculateAges(movie.Released, movie.Actors)` is an expression, EntityGraphQL can rewrite it to work with the first execution result. It also can visit the expression tree to know that `Released` and `Actors` needs to be selected in that first execution.

If you use `ExecutionOptions.ExecuteServiceFieldsSeparately = false` you will need to make sure all data is available / handle possible `null`s.
