---
title: 'Schema Creation'
metaTitle: 'Schema Creation - EntityGraphQL'
metaDescription: 'Creating a GraphQL schema with EntityGraphQL'
---

EntityGraphQL supports customizing your GraphQL schema in all the expected ways;

- Adding/removing/modifying fields
- Adding optional/required arguments to fields
- Adding new types (including input types)
- Adding mutations to modify data
- Including data from multiple sources

# Schema Creation

To create a new schema we need to supply a base context type.

```
// DemoContext is our base context for the schema.
// Schema has no types or fields yet
var schema = new SchemaProvider<DemoContext>();
```

# Adding Types

Now we need to add some types to our schema which we will use as return types for fields. The most common GraphQL types you will deal with are

- Object types - a type that is part of the object graph and has fields. These are the most common type you will use in your schema
- Scalar types - An Object type has fields that can be queried. Scalar types resolve to concrete data. GraphQL spec defines the following built in scalar types (of course you can add your own)
  - Int: A signed 32‐bit integer.
  - Float: A signed double-precision floating-point value.
  - String: A UTF‐8 character sequence.
  - Boolean: true or false.
  - ID: The ID scalar type represents a unique identifier, often used to refetch an object or as the key for a cache. The ID type is serialized in the same way as a String; however, defining it as an ID signifies that it is not intended to be human‐readable.
    Types are a just a name and a list of fields on that type. This lets EntityGraphQL know how to map a GraphQL type back to a .NET type.
- Enumeration types - enumeration types are a special kind of scalar that is restricted to a particular set of allowed values

For more information of GraphQL types visit the [GraphQL docs](https://graphql.org/learn/schema/#type-system).

```
schema.AddType<Person>("Person", "Hold data about a person object");
```

# Adding Fields

We now need to add some fields to both the root query object and our new `Person` Object Type.

```
schema.UpdateType<Person>(personType => {
    personType.AddField(
        "firstName", // name in graphql schema
        person => person.FirstName, // expression to resolve the field on the .NET type
        "A person's first name" // description of the field
    );
});
```

The resolve expression can be any expression you can build.

```
schema.UpdateType<Person>(personType => {
    personType.AddField(
        "fullName",
        person => $"{person.FirstName} {person.LastName}",
        "A person's full name"
    );
});
```

Now let's add a root query field so we can query people.

```
schema.AddField(
    "people",
    ctx => ctx.People, // ctx is the core context used when creating the schema above
    "List of people"
);
```

We now have a very simple GraphQL schema ready to use. It has a single root query field (`people`) and a single type `Person` with 2 fields (`firstName` & `fullName`).

# Helper Methods

EntityGraphQL comes with some methods to speed up the creation of your schema. This is helpful to get up and running but be aware if you are exposing this API externally it can be easy to make breaking API changes. For example using the methods above if you end up changing the underlying .NET types you will have compilation errors which alert you of breaking API changes and you can address them. Using the methods below will automatically pick up the underlying changes of the .NET types.

## Building a full schema

```
// Automatically add all types and fields from the base context
var schema = SchemaBuilder.FromObject<DemoContext>(
    autoCreateIdArguments: true,
    autoCreateEnumTypes: true,
    fieldNamer: null
);
```

Explaining the arguments:

- `autoCreateIdArguments` - for any root level fields that return a list of an Object Type that has a field called `Id`, it will create a singular field in the schema with an `id` argument. For example the `DemoContext` used in Getting Started the `DemoContext.People` will create the following GraphQL schema

```
schema {
    query: Query
}

Type Query {
    people: [Person]
    person(id: ID!): Person
}

Type Person {
    firstName: String
    ...
}
```

- `autoCreateEnumTypes` - automatically create Enum types in the schema if found in the `DemoContext` object graph
- `fieldNamer` - A `Func<string, string>` lambda used to generate field names. The default `fieldNamer` adopts the GraphQL standard of naming fields `lowerCamelCase`

## Adding all fields on a type

`AddAllFields()` on the schema type will automatically add all the fields on that .NET type.

```
schema.AddType<Person>("Person", "All about the project")
    .AddAllFields(
        autoCreateNewComplexTypes: false,
        autoCreateEnumTypes: true
    );
```

- `autoCreateNewComplexTypes` - If there is a field that returns another custom .NET type this will create a whole new GraphQL type for it

## Modifying the generated schema

EntityGraphQL provides method to help you modify a schema as well.

```
schema.UpdateType<Person>(personType => {
    personType.RemoveField("firstName");
    personType.ReplaceField(
        "lastName",
        p => p.LastName.ToUpper(), // new expression to resolve the lastName field
        "New description"
    );
});

schema.RemoveType<TType>();
schema.RemoveType("TypeName");

// Remove a type and all fields that return that type
schema.RemoveTypeAndAllFields<Type>();
```
