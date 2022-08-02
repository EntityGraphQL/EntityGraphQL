---
sidebar_position: 4
---

# Enum Types

[Enum types](https://graphql.org/learn/schema/#enumeration-types) are just like you'd expect. It let's API consumers know that a field can be only 1 of a set of values.

With our `Person` example we could add a `Gender` enum.

```cs
[JsonConverter(typeof(StringEnumConverter))]
public enum Gender {
    Female,
    Male,
    NotSpecified
}

// building our schema
schema.AddEnum("Gender", typeof(Gender), "A persons Gender");
```

The GraphQL schema produced from this helps document and describe the data model to API users. Example GraphQL schema below

```graphql
enum Gender {
  Female
  Male
  NotSpecified
}

type Person {
  firstName: String
  lastName: String
  gender: Gender
}
```
