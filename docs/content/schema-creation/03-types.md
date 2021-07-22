---
title: "Other Types"
metaTitle: "Advanced types - EntityGraphQL"
metaDescription: "Advanced types in EntityGraphQL"
---

So far we've only been dealing with GraphQL Object types. Types that are part of the object graph and have fields. These are the most common type in our schema, but lets look at the other types we can use.

# Scalar types
We learnt previous that the GraphQL spec defines the following built in scalar types.

- Int: A signed 32‐bit integer.
- Float: A signed double-precision floating-point value.
- String: A UTF‐8 character sequence.
- Boolean: true or false.
- ID: The ID scalar type represents a unique identifier. The ID type is serialized in the same way as a String; however, defining it as an ID signifies that it is not intended to be human‐readable.

We of course can add our own. Scalar types help you describe the data in you schema. Unlike Object types they don't have fields you can query, they result in data. Ultimately you are likely serializing the data to JSON for transport.

```
```

# Enum types


# Input types
