---
sidebar_position: 9
---

# Adding Other Data Sources

EntityGraphQL lets you add fields that resolve data from other sources other than the core context you created your schema with. This is powerful as it let's you create a single API that brings together multiple data sources into an object graph.

## `ResolveWithService<TService>()` Fields

To use other services in a field we use the `ResolveWithService<TService>()` method on the field. EntityGraphQL uses the `IServiceProvider` you pass on execution to resolve the services.

Let's use a service in our `Person` type example.

```cs
schema.UpdateType<Person>(personType => {
    personType.AddField("age", "Person's age")
        .ResolveWithService<IAgeService>((person, srv) => srv.GetAge(person.Dob));
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

Now when someone requests the `age` field on a person the result will be resolved by executing the service `srv.GetAge(person.Dob)`. Of course you could calculate the age directly in the field's resolve expression without a service but this demonstrates how to use other services.

## Services With Non-Scalar Types

A service can return any type. If it is a complex type you will need to add it to the schema.

```cs
schema.Query().AddField("users", "Get list of users")
    .ResolveWithService<IUserService>(
        // ctx is the core context we created the schema with. For this field we don't use it
        (ctx, srv) => srv.GetUsers(),
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

With the User example above you might want to add fields to the `User` type that brings in data back from the core context. This let's you create a rich deep object graph for querying.

When joining non-core context types back to the core context you need to use `ResolveWithService()` again.

```cs
schema.UpdateType<User>(userType => {
    userType.AddField("tasks", "List of projects assigned to the user")
        .ResolveWithService<DemoContext>(
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

Let's say we want a root-level `metrics` field where each sub-field uses a service to load the value. If someone only queries 1 of the fields you don't want all of them to resolve. To achieve this we can do the following.

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
    .ResolveWithService<IMetricService>(
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

To demonstrate that only the `m.TotalWebhooks()` method is called here is what is produced as the .NET expression.

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
