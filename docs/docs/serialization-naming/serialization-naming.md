# Serialization & Field Naming

_GraphQL is case-sensitive_. Currently EntityGraphQL will automatically turn field and argument names from `UpperCase` to `camelCase` when you use the helper methods to create a schema with the default options. This means your C# code matches what C# code typically looks like and your GraphQL matches the GraphQL norm too.

Examples:

- A mutation method in C# named `AddMovie` will be `addMovie` in the schema
- A root field entity named `Movie` will be named `movie` in the schema
- A mutation arguments class (`ActorArgs`) with fields `FirstName` & `Id` will be arguments in the schema as `firstName` & `id`
- If you're using the schema builder manually, the names you give will be the names used. E.g. `schemaProvider.AddField("someField", ...)` is different to `schemaProvider.AddField("SomeField", ...)`

## Override default naming

To override the default behaviour you can pass in your own `fieldNamer` function when creating the `SchemaProvider` or configuring it.

```
services.AddGraphQLSchema<DemoContext>(options => {
    options.FieldNamer = name => name; // use the dotnet name as is
});
```

Then make sure you follow your naming policy when adding fields to the schema.

```
services.AddGraphQLSchema<DemoContext>(options => {
    options.FieldNamer = name => name; // use the dotnet name as is
    options.ConfigureSchema = schema => {
        schema.Query().AddField("SomeField", ...)
    };
});
```

**Note that this impacts the names used for fields and arguments in the GraphQL schema and how these are matched to a query. This can impact serialization, but is not serialization.**

An example - our `DemoContext` with the default `fieldNamer` will create this GraphQL schema (trimmed down for the example). Note the `camelCase` naming.

```
schema {
    query: Query
}

Type Query {
    people: [Person]
    person(id: ID!): Person
}

Type Person {
    id: ID!
    firstName: String!
    ...
}
```

This means queries need to match the casing as GraphQL is case-sensitive.

```
{
  # will work
  people {
    id
    firstName
  }

  # will fail
  People {
    id
    firstName
  }
}
```

The above query will generate typed objects _before serialization_ with the matching names from the schema. _Note the types are generated internally as part of compiling, users do not need to use/know them but it demostrates serialization impact_.

```
public class TempPersonResult
{
    // names taken from schema naming - e.g. camelCase
    public Guid id;
    public string firstName;
}
```

The `QueryResult` object is a dictionary of root level queries `{ fieldName: object }` and in the above case it would be

```
IEnumerable<TempPersonResult> people = ... // implementation is more complex and not shown here - see Entity Framework section for more info
QueryResult result = ctx => {
    {"people", people} // key taken from schema fiel name - camelCase
}
```

## Serialization

We see how types/fields are named by default above and how to change that. Next you may want to change how data comming in or returned is deserialized or serialized.

You can customise how EntityGraphQL handles de/serialization by registering your own `IGraphQLResponseSerializer` and/or `IGraphQLRequestDeserializer`. Both should be registered before calling `AddGraphQLSchema()` as it adds the default implementations if not already registered.

- `IGraphQLRequestDeserializer` - Used to deserialize an incoming `POST` body data into a `QueryRequest` object
- `IGraphQLResponseSerializer` - Used to serialize a `QueryResult` object into the `Response` stream

The default implementations try to deserialize JSON into the `QueryRequest` object and serialize the `QueryResult` object to JSON using the following JSON options.

```
var jsonOptions = new JsonSerializerOptions
{
    // the internal generated types use fields so include this
    IncludeFields = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // match common JSON style and fits with many GraphQL tools
};
// Convert ENUMs to their string names
var jsonOptions.Converters.Add(new JsonStringEnumConverter());
```

You can quickly overwrite the default JSON options without implementing your own `IGraphQLResponseSerializer` and/or `IGraphQLRequestDeserializer`.

```
var jsonOptions = new JsonSerializerOptions
{
    // the internal generated types use fields so include this
    IncludeFields = true,
    // leaving out the camelCase option
};
// overwrite the JSON options using the DefaultGraphQLResponseSerializer
services.AddSingleton<IGraphQLResponseSerializer>(new DefaultGraphQLResponseSerializer(jsonOptions));
services.AddGraphQLSchema<DemoContext>();
```

## Full PascalCase example

```
var jsonOptions = new JsonSerializerOptions
{
    // the internal generated types use fields so include this
    IncludeFields = true,
};
var jsonOptions.Converters.Add(new JsonStringEnumConverter());
services.AddSingleton<IGraphQLRequestDeserializer>(new DefaultGraphQLRequestDeserializer(jsonOptions));
services.AddSingleton<IGraphQLResponseSerializer>(new DefaultGraphQLResponseSerializer(jsonOptions));

services.AddGraphQLSchema<DemoContext>(options =>
{
    options.FieldNamer = name => name;
});
```

The above expects a JSON request like

```
{
    "Query": "query { People { Id FirstName } }"
}
```

And a JSON result like

```
{
    "People": [
        {
            "Id": "123",
            "FirstName": "Bob"
        },
        ...
    ]
}
```
