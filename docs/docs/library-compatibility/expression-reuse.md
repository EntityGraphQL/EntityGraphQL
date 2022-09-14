# Reusing Linq Expressions

Computed properties don't work with most Linq providers out of the box. Dave Glick has a great related post on [Computed Proprties and EntityFramework](https://www.daveaglick.com/posts/computed-properties-and-entity-framework).

Here's a list of various libraries that can help

* [EntityFrameworkCore.Projectables](https://github.com/koenbeuk/EntityFrameworkCore.Projectables)
* [Nein Linq](https://nein.tech/nein-linq/)
* [Expressionify](https://github.com/ClaveConsulting/Expressionify)
* [LinqKit](https://github.com/scottksmith95/LINQKit)
* [Delegate Decompiler](https://github.com/hazzik/DelegateDecompiler)

Most the these libraries work in a similar ways - examples below are using the syntax from EntityFrameworkCore.Projectables.

1) Either globally register the library when registering the context 

```
services.AddDbContext<DemoContext>(opt => opt.UseSqlite("Filename=demo.db").UseProjectables());
```

or enable it per expression.

```
dbContext.People  
  .ExpandProjectables() 
  .Select(x => x.Age())
```

If you go down the route of registering it per query then you'll need to override the resolve method on the relevant fields on the EQL Schema.


2) Add an expression bodied function to the entityclass or helper class (generally be static or instance) and mark it using an attribute.

```
[Projectable]
public static int Age(this Person person) => (int)((DateTime.Now - person.Dob).TotalDays / 365);
```

Some libraries will automatically convert this to an expression property using Source Generators (EFC.Projectables, Expressionfy) or reflection/decompilation (Delegate Decompiler), yet others require you to provide both the method and expression yourself (Nein Linq).

As EntityGraphQL does not support auto registering methods as fields you will need to manually update the schema with this mapping

```
  type.AddField(
    "age",
    (person) => person.Age(),
    "Show the person's age"
  );
```

