---
title: 'Entity Framework'
metaTitle: 'Working with Entity Framework - EntityGraphQL'
metaDescription: 'Working with Entity Framework & GraphQL'
---

EntityGraphQL is built to work extremely well with EntityFramework. To see how let's first look at what EntityGraphQL does with GraphQL queries.

# Examples

Using the `DemoContext` and the schema we created from the Getting Started section, lets look at the sample queries.

```
query {
    movie(id: 11) {
        id name
    }
}
```

EntityGraphQL parses the GQL document into an internal representation and uses the schema we built to construct a .NET expression. It will look like this.

```
var expression = (DemoContext ctx, AnonymousType<> args) =>
    ctx.Movies
        .Where(movie => movie.Id == args.id)
        .Select(movie => new {
            id = movie.Id,
            name = movie.Name
        })
        .FirstOrDefault();
```

You can see all we need to execute this expression is an instance of `DemoContext` and the `args` object which is built by EntityGraphQL on parsing of the GQL document. Given those things EntityGraphQL can do similar to this.

```
var results = expression.Compile().DynamicInvoke(demoContextInstance, argInstance);
```

Now if your `DemoContext` is built on top of Entity Framework `DbContext` when EntityGraphQL executes the expression EF will take over and do its thing!

Namely note that EntityGraphQL only selects the fields asked for and therefore EF will also only return the fields we ask for. Meaning no over fetching to the DB either. If your table had many fields and some large ones, they are not selected from the DB unless the API user asks for those fields.

Let's look at a more complicated example.

```
{
    movies {
        id
        name
        director {
            name
        }
        writers {
            name
        }
    }
}
```

Will result in the following expression.

```
var expression = (DemoContext ctx) =>
    ctx.Movies
        .Select(movie => new {
            id = movie.Id,
            name = movie.Name,
            director = new {
                name = movie.Director.Name
            },
            writers = movie.Writers.Select(writer => new {
                name = writer.Name
            })
        });
```

Again, EF will take over and fetch your data for you.

You'll note that EntityGraphQL doesn't care what the context is. It could be a object graph 100% held in memory. What does matter is that when the expression executes and resolves something like `movie.Writers.Select()` that the object has the expected data loaded, or like EF can resolve the data.

Other ORMs built on top of `LinqProvider` and `IQueryable` should also work although have not been tested.

# How EntityGraphQL handles ResolveWithService() with EF

Since using EntityGraphQL against an Entity Framework Core `DbContext` is supported we handle `ResolveWithService()` in a way that will work with EF Core (and possibly other IQueryable based ORMs) which allows EF to build an optimal SQL statement. EF core 3.1+ will throw an error by default if it can't translate an expression to SQL. It can't translate the services used in `ResolveWithService()` to SQL. To support EF 3.1+ performing optimal queries (and selecting only the fields you request) EntityGraphQL builds and executes the expressions in 2 parts.

_This can be disabled by setting the argument `ExecuteServiceFieldsSeparately` when executing to `false`. For example if your core context is an in memory object._

If you encounter any issues when using `ResolveWithService()` on fields and EF Core 3.1+ please raise an issue.

Example of how EntityGraphQL handles `ResolveWithService()`, which can help inform how you build/use other services.

Given the following GQL

```
{
  people { age manager { name } }
}
```

Where `age` is defined with a service as

```
schema.Type<Person>().AddField("age", "Persons age")
    .ResolveWithService<AgeService>((ager) => ager.GetAge(person.Birthday));
```

EntityGraphQL will build an expression query that first selects everything from the base context (`DemoContext` in this case) that EF can execute. Then another expression query that runs on top of that result which includes the `ResolveWithService()` fields. This means EF can optimise your query and return all the data requested (and nothing more) and in memory we then merge that with data from your services.

An example in C# of what this ends up looking like.

```
var dbResultFunc = (DbContext context) => context.People.Select(p => new {
    Birthday = p.Birthday, // extracted from the ResolveWithService expression as it is needed in the in-memory resolution
    manager = new {
        name = p.Manager.Name
    }
})
.ToList(); // EF will fetch data
var dbResult = dbResultFunc(dbContext); // executes the expression

// note dbResult is an anonymous type known at runtime
var resultsFunc = (AnonType dbResult, AgeService ager) => dbResult.Select(p => {
    age = ager.GetAge(p.Birthday)), // passing in data we selected just for this
    manager = p.manager // simple selection from the previous result
})
.ToList();
var results = resultsFunc(dbResult, ager); // execute for the final result
```

This allows EF Core to make it's optimizations and prevent over fetching of data when using EntityGraphQL against an EF DbContext.

# Limitations with using `ResolveWithService()` & EF

If you are using the above functionality where the query will be completed in 2 parts, below are the current limitations to think about when building fields using services.

Do not traverse through a relation in your field expression that uses `ResolveWithService()`.

An example of what will not work.

```
schema.UpdateType<Floor>(type => {
  type.AddField("floorUrl", "Current floor url")
    .ResolveWithService<IFloorUrlService>((s) => s.BuildFloorPlanUrl(f.SomeRelation.FirstOrDefault().Id));
});
```

This will trigger the expression we build for EF to select `floor.SomeRelation` which errors in EF because of [this issue](https://github.com/dotnet/efcore/issues/23205) (or related).

For now you can modify you field to only select fields on the context and update the service to load anything it needs to return the correct data. Remember you services can access the DB context or anything else it needs via DI as well.

```
schema.UpdateType<Floor>(type => {
  type.AddField("floorUrl", "Current floor url")
    .ResolveWithService<IFloorPlanUrlService>((s) => s.BuildFloorUrlFromFloorId(f.Id));
});
```
