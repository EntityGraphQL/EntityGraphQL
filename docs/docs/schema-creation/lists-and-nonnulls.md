---
sidebar_position: 8
---

# Lists and Non-Null

GraphQL defines [type modifiers](https://graphql.org/learn/schema/#lists-and-non-null) specifically for declaring that a field is a list or cannot be `null`. In a schema these are `[T]` and `!`. For example, a GraphQL schema might have the following.

```graphql
enum Gender {
  Female
  Male
  NotSpecified
}

type Person {
  firstName: String!
  lastName: String!
  gender: Gender!
  friends: [Person]
}
```

The `!` on all the fields tells API users that those fields will not be `null`. And the `[]` around the `Person` type on the `friends` field types the API users that the field returns a list of `Person` objects.

EntityGraphQL will automatically figure out fields that return lists based on if the resolve expression returns `IEnumerable<T>`.

Similarly, EntityGraphQL will mark non-nullable .NET types as non-null in thr GraphQL schema. If you need to change that, you can use `field.IsNullable(bool)`.

Lets say we know a person's first and last name will never be null.

```cs
var type = schema.AddInputType<Person>("PersonInput", "New person data")
type.AddField("firstName", p => p.FirstName, "First name).IsNullable(false);
type.AddField("lastName", p => p.LastName, "Last name).IsNullable(false);
```
