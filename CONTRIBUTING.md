Yes please :)

Check out any open issues, or dive in and add some functionality. Below I'll try to outline a few interesting parts of the code base.

# Requirements

- Dotnet 6 or higher
  - EntityGraphQL library still targets netstandard2.1
  - EntityGraphQL.AspNet targets `net6.0` and `net7.0`
- Java/Antlr4 - The expression language used in the `UseFilter()` field extension uses a grammer defined with Antlr4. The visitor / AST code is generated at compile time from the `EntityQL.g4` grammer file. This requires the `antlr4` command available which requires Java. This is only to generate the C# code - not at runtime.

# Schema Building

`SchemaProvider` holds the mapping from the GraphQL schema to the dotnet types. It is the interface users use to build out thier schema. Users will add different types and fields to those types.

GraphQL has 3 top level special types, the `Query`, `Mutation` & `Subscription`.

The `TContextType` in the `SchemaProvider` is the core context that the `Query` type is built from.

Each type has a list of `Field`s. The `Mutation` type is implemented differently as we execute it differently but the core idea is it is still a type with a list of fields.

## Key classes

- `SchemaProvider`
- `Field` & `MutationField`
- `SchemaType` & `MutationType`

# Compiling a QueryRequest

## GraphQLCompiler

`GraphQLCompiler` - runs everything. First it compiles the GraphQL document into an AST and then it uses the `EntityGraphQLQueryWalker` to walk that tree and build the resulting expression.

## EntityGraphQLQueryWalker

In `EntityGraphQLQueryWalker` we visit all the GraphQL tokens defined in the grammer and process them. Basically we turn them into .Net expressions.

Example, GQL

```gql
query {
  movies {
    id
    name
  }
  actors {
    name
    age
  }
}
```

The final result of `EntityGraphQLQueryWalker` is a `GraphQLDocument` which holds each top level operation (`ExecutableGraphQLStatement` for operations) and any fragments (`GraphQLFragmentStatement`).

An `ExecutableGraphQLStatement` is an abstract class and will be either a query (`GraphQLQueryStatement`) or a mutation (`GraphQLMutationStatement`) or a subscription (`GraphQLSubscriptionStatement`). These classes handle the execution of the whole expression against the data context.

Each will have `QueryFields` which is a list of `BaseGraphQLField`. The following inherit from `BaseGraphQLField`.

## GraphQLFragmentField

`GraphQLFragmentField` represents a fragment selection. The fragment is defined in the `GraphQLDocument`.

## GraphQLScalarField

`GraphQLScalarField` represents a value. i.e. the field can not be queried further. E.g. a number, a string or a scalar type defined in the schema.

## BaseGraphQLQueryField

`BaseGraphQLQueryField` is the base class for fields that we can queried.

## GraphQLMutationField

`GraphQLMutationField` is a mutation field/call (within the mutation statement). e.g.

```gql
mutation MyMutation {
    thisIsTheField("hi") { name }
}
```

It holds the selection of the result (`{ name }` above) and wraps up the execution the mutation and the final result seleciton against the mutation result.

## GraphQLSubscriptionField

`GraphQLSubscriptionField` similar to the mutation fiel above, holding the selection query for subscription updates.

## GraphQLObjectProjectionField

`GraphQLObjectProjectionField` handles the building of expressions on fields that return objects (vs. lists). For example if your field returns a single `User` type and you have a GQL selection on it (`user { id name }`), this class builds the .NET expressions. It handles null checking and other cases.

## GraphQLListSelectionField

`GraphQLListSelectionField` handles the building of expressions on fields that return lists/arrays. For example if your field returns a list of `User` types and you have a GQL selection on it (`users { id name }`), this class builds the .NET expressions.

## GraphQLCollectionToSingleField

`GraphQLCollectionToSingleField` handles a case where a field may be built from a root list.

For example:

```gql
query {
  movie(id: 1) {
    id
    name
  }
}
```

This field may be defined as

```c#
(ctx, id) => ctx.Movies.FirstOrDefault(m => m.Id == id)
```

`GraphQLCollectionToSingleField` will inject the selection of the fields before the `FirstOrDefault`, providing better support for ORMs like EF to not over fetch data. e.g.

```c#
(ctx, id) => ctx.Movies
    .Select(m => new {
        id = m.Id,
        name = m.Name
    })
    .FirstOrDefault(m => m.id == id)
```

# Where to start?

`ExecutableGraphQLStatement.CompileAndExecuteNodeAsync()` is a good place to add a breakpoint and step through.
