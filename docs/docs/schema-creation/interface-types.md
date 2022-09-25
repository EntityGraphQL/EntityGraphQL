---
sidebar_position: 6
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