Yes please :)

Check out any open issues, or dive in and add some functionality. Below I'll try to outline a few interesting parts of the code base.

# GraphQLCompiler
`GraphQLCompiler` - runs everything. First it compiles the GraphQL document into an AST and then it uses the `EntityGraphQLQueryWalker` to walk that tree and build the resulting expression.

# EntityGraphQLQueryWalker
In `EntityGraphQLQueryWalker` we visit all the GraphQL tokens defined in the grammer and process them. Basically we turn them into .Net expressions.

Example, GQL
```gql
query {
    movies {
        id name
    }
    actors { name age }
}
```

The final result of `EntityGraphQLQueryWalker` is a `GraphQLDocument` which holds each top level operation (`ExecutableGraphQLStatement` for operations) and any fragments (`GraphQLFragmentStatement`).

An `ExecutableGraphQLStatement` is an abstract class and will be either a query (`GraphQLQueryStatement`) or a mutation (`GraphQLMutationStatement`). These classes handle the execution of the whole expression against the data context.

Each will have `QueryFields` which is a list of `BaseGraphQLField`. The follow inherit from `BaseGraphQLField`.

# GraphQLFragmentField
`GraphQLFragmentField` represents a fragment selection. The fragment is defined in the `GraphQLDocument`.

# GraphQLScalarField
`GraphQLScalarField` represents a value. i.e. the field can not be queried further. E.g. a number, a string or a scalar type defined in the schema.

# BaseGraphQLQueryField
`BaseGraphQLQueryField` is the base class for fields that we can queried.

## GraphQLMutationField
`GraphQLMutationField` is a mutation field/call (within the mutation statement). e.g.

```gql
mutation MyMutation {
    thisIsTheField("hi") { name }
}
```

It holds the selection of the result (`{ name }` above) and wraps up the execution the mutation and the final result seleciton against the mutation result.

## GraphQLObjectProjectionField
`GraphQLObjectProjectionField` handles the building of expressions on fields that return objects (vs. lists). For example if your field returns a single `User` type and you have a GQL selection on it (`user { id name }`), this class builds the .NET expressions. It handles null checking and other cases.

## GraphQLListSelectionField
`GraphQLListSelectionField` handles the building of expressions on fields that return lists/arrays. For example if your field returns a list of `User` types and you have a GQL selection on it (`users { id name }`), this class builds the .NET expressions.

## GraphQLCollectionToSingleField
`GraphQLCollectionToSingleField` handles a case where a field may be built from a root list.

For example:

```gql
query {
    movie(id: 1) { id name }
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

`ExecutableGraphQLStatement.CompileAndExecuteNode()` is a good place to add a breakpoint and step through.
