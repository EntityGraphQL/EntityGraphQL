# Reusing Linq Expressions

Computed properties don't work with most Linq providers out of the box. Dave Glick has a great related post on [Computed Proprties and EntityFramework](https://www.daveaglick.com/posts/computed-properties-and-entity-framework).

Here's a list of various libraries that can help

- [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables)
- [Nein Linq](https://nein.tech/nein-linq/)
- [Expressionify](https://github.com/ClaveConsulting/Expressionify)
- [LinqKit](https://github.com/scottksmith95/LINQKit)
- [Delegate Decompiler](https://github.com/hazzik/DelegateDecompiler)

Most the these libraries work in a similar ways - examples below are using the syntax from [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables).

1. Either globally register the library when registering the context

```cs
services.AddDbContext<DemoContext>(opt =>
  opt.UseSqlite("Filename=demo.db")
  // highlight-next-line
    .UseProjectables()
);
```

or enable it per expression.

```cs
dbContext.People
// highlight-next-line
  .ExpandProjectables()
  .Select(x => x.Age())
```

If you go down the route of registering it per query then you'll need to override the resolve method on the relevant fields on the EQL Schema.

2. Add an expression bodied function to the entityclass or helper class (generally be static or instance) and mark it using an attribute.

```cs
[Projectable]
[GraphQLField]
public static int Age(this Person person) => (int)((DateTime.Now - person.Dob).TotalDays / 365);
```

Some libraries will automatically convert this to an expression property using Source Generators (EFC.Projectables, Expressionfy) or reflection/decompilation (Delegate Decompiler), yet others require you to provide both the method and expression yourself (Nein Linq).

This field is now available to queries (exposed via the `[GraphQLField]` attribute). `Projectable`s provide reuse of common expression that can also be used outside on EntityGraphQL or in your mutations as well.

```cs
public class PeopleMutations
{
    [GraphQLMutation("Add a new person to the system")]
    public Expression<Func<DemoContext, Person>> UpdatePerson(DemoContext db, int id, string firstName, string lastName)
    {
        var person = db.People.Select(p => {
            p.FirstName,
            p.LastName,
// highlight-next-line
            p.Age()
        });

        ...
    }
}
```
