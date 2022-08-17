---
sidebar_position: 3
---

# Scalar Types

We learnt previously that the [GraphQL spec](https://graphql.org/learn/schema/#scalar-types) defines the following built in scalar types.

- Int: A signed 32‐bit integer.
- Float: A signed double-precision floating-point value.
- String: A UTF‐8 character sequence.
- Boolean: true or false.
- ID: The ID scalar type represents a unique identifier. The ID type is serialized in the same way as a String; however, defining it as an ID signifies that it is not intended to be human‐readable.

We of course can add our own. Scalar types help you describe the data in you schema. Unlike Object types they don't have fields you can query, they result in data. Ultimately you are likely serializing the data to JSON for transport.

Adding a scalar type tells EntityGraphQL that the object should just be returned. i.e. there is no selection available on it. A good example is `DateTime`. We just want to return the `DateTime` value. Not have it as an Object type where you could select certain properties from it (Although you could set that up).

```cs
schema.AddScalarType<DateTime>("DateTime", "Represents a date and time.");
```

EntityGraphQL by default will set up the follow scalar types on schema creation.

```cs
new SchemaType<int>(this, "Int", "Int scalar", null, GqlTypeEnum.Scalar);
new SchemaType<double>(this, "Float", "Float scalar", null, GqlTypeEnum.Scalar);
new SchemaType<bool>(this, "Boolean", "Boolean scalar", null, GqlTypeEnum.Scalar);
new SchemaType<string>(this, "String", "String scalar", null, GqlTypeEnum.Scalar);
new SchemaType<Guid>(this, "ID", "ID scalar", null, GqlTypeEnum.Scalar);
new SchemaType<char>(this, "Char", "Char scalar", null, GqlTypeEnum.Scalar);
new SchemaType<DateTime>(this, "Date", "Date with time scalar", null, GqlTypeEnum.Scalar);
new SchemaType<DateTimeOffset>(this, "DateTimeOffset", "DateTimeOffset scalar", null, GqlTypeEnum.Scalar)
```

It is best to have scalar types added to the schema before adding other fields that reference them. Otherwise EntityGraphQL doesn't know about the scalar types. You can add you're own or make changes to the default when registering your schema.

```cs
services.AddGraphQLSchema<TContext>(options => {
  options.PreBuildSchemaFromContext = schema =>
  {
      // remove and/or add scalar types or mappings here. e.g.
      schema.RemoveType<DateTime>();
      schema.AddScalarType<KeyValuePair<string, string>>("StringKeyValuePair", "Represents a pair of strings");
  };
})
```

You can also tell EntityGraphQL to auto-map a dotnet type to a schema type with `AddTypeMapping<TFromType>(string gqlType)`. For example

```cs
schema.AddTypeMapping<short>("Int");
```

By default EntityGraphQL maps these types to GraphQL types (Note `int`, `bool`, etc are not here as they are added as scalar types in the schema above).

```cs
sbyte   ->  Int
short   ->  Int
ushort  ->  Int
long    ->  Int
ulong   ->  Int
byte    ->  Int
uint    ->  Int
float   ->  Float
decimal ->  Float
byte[]  ->  String
```
