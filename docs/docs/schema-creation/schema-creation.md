import DocCardList from '@theme/DocCardList';
import {useCurrentSidebarCategory} from '@docusaurus/theme-common';

# Schema Creation

EntityGraphQL supports customizing your GraphQL schema in all the expected ways;

- Adding/removing/modifying fields
- Adding optional/required arguments to fields
- Adding new types (including input types)
- Adding mutations to modify data
- Including data from multiple sources

To create a new schema we need to supply a base context type.

```cs
// DemoContext is our base query context for the schema.
// Schema has no types or fields yet
var schema = new SchemaProvider<DemoContext>();
```

## Adding Types

Now we need to add some types to our schema which we will use as return types for fields. The most common GraphQL types you will deal with are

- Object types - a type that is part of the object graph and has fields. These are the most common type you will use in your schema
- Input object types - Like Object types but are strictly used for input object for field or mutation arguments. The main difference is thata fields on an Input object type can not have arguments
- Scalar types - An Object type has fields that can be queried. Scalar types resolve to concrete data. GraphQL spec defines the following built in scalar types (of course you can add your own)
  - Int: A signed 32-bit integer.
  - Float: A signed double-precision floating-point value.
  - String: A UTF-8 character sequence.
  - Boolean: true or false.
  - ID: The ID scalar type represents a unique identifier, often used to refetch an object or as the key for a cache. The ID type is serialized in the same way as a String; however, defining it as an ID signifies that it is not intended to be human-readable.
    Types are a just a name and a list of fields on that type. This lets EntityGraphQL know how to map a GraphQL type back to a .NET type.
- Enumeration types - enumeration types are a special kind of scalar that is restricted to a particular set of allowed values

For more information of GraphQL types visit the [GraphQL docs](https://graphql.org/learn/schema/#type-system).

To register a type in the schema:

```cs
schema.AddType<Person>("Person", "Hold data about a person object");
```

This will add the `Person` type as a schema _object type_ named `Person`. It does not have fields yet.

## Adding Fields

We now need to add some fields to both the root query object and our new `Person` Object Type.

```cs
schema.UpdateType<Person>(personType => {
    personType.AddField(
        "firstName", // name in GraphQL schema
        person => person.FirstName, // expression to resolve the field on the .NET type
        "A person's first name" // description of the field
    );
});
```

The resolve expression can be any expression you can build.

```cs
schema.UpdateType<Person>(personType => {
    personType.AddField(
        "fullName",
        person => $"{person.FirstName} {person.LastName}",
        "A person's full name"
    );
});
```

Now let's add a root query field so we can query people.

```cs
schema.Query() // returns the root GraphQL query type
.AddField(
    "people",
    ctx => ctx.People, // ctx is the core context used when creating the schema above
    "List of people"
);
```

We now have a very simple GraphQL schema ready to use. It has a single root query field (`people`) and a single type `Person` with 2 fields (`firstName` & `fullName`).

## Helper Methods

EntityGraphQL comes with some methods to speed up the creation of your schema. This is helpful to get up and running but be aware if you are exposing this API externally it can be easy to make breaking API changes. For example using the methods above if you end up changing the underlying .NET types you will have compilation errors which alert you of breaking API changes and you can address them. Using the methods below will automatically pick up the underlying changes of the .NET types.

### Building a full schema

```cs
// Automatically add all types and fields from the base context
var schema = SchemaBuilder.FromObject<DemoContext>();
```

Optional arguments for the schema builder:

1. `SchemaBuilderSchemaOptions` - options that get passed to the created schema
   - `.FieldNamer` - A `Func<string, string>` lambda used to generate field names. The default `fieldNamer` adopts the GraphQL standard of naming fields `lowerCamelCase`
   - `.IntrospectionEnabled` - Weather or not GraphQL query introspection is enabled or not for the schema. Default is `true`
   - `.AuthorizationService` - An `IGqlAuthorizationService` to control how auth is handled. Default is `RoleBasedAuthorization`
   - `.PreBuildSchemaFromContext` - Called after the schema object is created but before the context is reflected into it. Use for set up of type mappings or anything that may be needed for the schema to be built correctly.
   - `.IsDevelopment` - If `true` (default), all exceptions will have their messages rendered in the 'errors' object. If `false`, exceptions not included in `AllowedExceptions` will have their message replaced with 'Error occurred'
   - `.AllowedExceptions` - List of allowed exceptions that will be rendered in the 'errors' object when `IsDevelopment` is `false`. You can also mark your exceptions with `AllowedExceptionAttribute`. These exceptions are included by default.

```cs
public List<AllowedException> AllowedExceptions { get; set; } = new List<AllowedException> {
    new AllowedException(typeof(EntityGraphQLArgumentException)),
    new AllowedException(typeof(EntityGraphQLException)),
    new AllowedException(typeof(EntityGraphQLFieldException)),
    new AllowedException(typeof(EntityGraphQLAccessException)),
    new AllowedException(typeof(EntityGraphQLValidationException)),
};
```

2. `SchemaBuilderOptions` - options used to control how the schema builder builds the schema

   - `.AutoCreateFieldWithIdArguments` - for any fields that return a list of an Object Type that has a field called `Id`, it will create a singular field in the schema with an `id` argument. For example the `DemoContext` used in Getting Started the `DemoContext.People` will create the following GraphQL schema. Default is `true`

     ```graphql
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

   - `.AutoCreateEnumTypes` - automatically create Enum types in the schema if found in the `DemoContext` object graph. Default is `true`
   - `.AutoCreateNewComplexTypes` - automatically add dotnet types found in the object graph to the schema. This will also call `AddAllFields()` on those types passing this options object to it. Default is `true`
   - `.AutoCreateInterfaceTypes` - If true (default = false), any object type that is encountered during reflection of the object graph that has abstract or interface types (regardless of if they are referenced by other fields), those will be added to the schema as an Interface including it's fields
   - `.IgnoreProps` - List properties or field names to ignore. Default includes a list of EF properties. Default list includes

   ```json
    "Database",
    "Model",
    "ChangeTracker",
    "ContextId"
   ```

   - `.IgnoreTypes` - List of type names to ignore when `AutoCreateNewComplexTypes = true`. Default is empty.
   - `.OnFieldCreated` - callback for each field that is created by the `SchemaBuilder`. Example usage is to apply something to all fields or all fields matching some criteria e.g. 

   ```cs
   // Add Sort to all list fields
   OnFieldCreated = (field) =>
    {
        if (field.ReturnType.IsList && field.ReturnType.SchemaType.GqlType == GqlTypes.QueryObject && !field.FromType.IsInterface)
        {
            field.UseSort();
        }
    }
   ```

### Adding all fields on a type

`AddAllFields()` on the schema type will automatically add all the fields on that .NET type.

```cs
schema.AddType<Person>("Person", "All about the project")
    .AddAllFields();
```

- `options` - you can configure how `AddAllFields` works with the properties of `SchemaBuilderOptions`

```cs
schema.AddType<Person>("Person", "All about the project")
    .AddAllFields(new SchemaBuilderOptions
    {
        AutoCreateNewComplexTypes = false, // do not add custom dotnet types found as property/field types to the schema. Only add scalar type fields
    });
```

## Modifying the generated schema

EntityGraphQL provides method to help you modify a schema as well.

```cs
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

## More details

See more details for each schema item in the following sections:
<DocCardList items={useCurrentSidebarCategory().items}/>
