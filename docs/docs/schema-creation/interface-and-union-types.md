---
sidebar_position: 5
---

# Interfaces Types & Implements Keyword

GraphQL supports [Interfaces](https://graphql.org/learn/schema/#interfaces) allowing you to define a abstract base type that multiple other types might implement.

EntityGraphQL automatically marks abstract classes and interfaces as GraphQL interfaces; however, you can also add them manually to a schema with the AddInterface method on the SchemaProvider class.

```cs
public abstract class Character {
    public int Id { get; set; }
    public string Name { get; set; }
    public IEnumerable<Character> Friends { get; set; }
    public IEnumerable<Episode> AppearsIn { get; set; }
}

public class Human : Character {
  public IEnumerable<Starship> starships { get; set; }
  public int TotalCredits { get; set;}
}

public class Droid : Character {
    public stirng PrimaryFunction { get; set;}
}

// creating our schema
schema.AddInterface<Character>(name: "Character", description: "represents any character in the Star Wars trilogy");
    .AddAllFields();

schema.AddType<Human>("")
    .AddAllFields()
    .Implements<Character>();

schema.AddType<Droid>("");
    .Implements<Character>();
```

produces the GraphQL schema:

```graphql
interface Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
}

type Human implements Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
  starships: [Starship]
  totalCredits: Int
}

type Droid implements Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
  primaryFunction: String
}
```

You can query these types with inline fragments;

```graphql
query {
  characters {
    name
    ... on Human {
      totalCredits
    }
    ... on Droid {
      primaryFunction
    }
  }
}
```

# Union Types
[Union Types](https://graphql.org/learn/schema/#union-types) are very similar to interfaces, but they don't get to specify any common fields between the types.

Any abstract class or interface automatically added by the SchemaBuilder that contains no properties is added as a union instead of an interface (interfaces require at least one field).

You can register union types manually using the `AddUnion` method on SchemaProvider, then register potential types on the union type using the `SchemaField.AddPossibleType` method.  This differs from interfaces in that you register the child classes on the parent instead of the parent on the children.

As C# does not support anything like union types they are implemented used blank 'marker interfaces'

```
public interface ICharacter { }
public class Human : ICharacter {
    ...
}
public class Droid : ICharacter {
    ...
}
// creating our schema
var union = schema.AddUnion<ICharacter>(name: "Character", description: "represents any character in the Star Wars trilogy");
        
    schema.Type<ICharacter>.AddPotentialType<Human>();
    schema.Type<ICharacter>.AddPotentialType<Droid>();
```