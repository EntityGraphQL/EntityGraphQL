Yes please :)

Check out any open issues, or dive in to add some functionality. Below I'll try to outline a few interesting parts of the code base.

# GraphQLCompiler
`GraphQLCompiler` just uses the generated parsers from Antlr4 to parse the GraphQL string. The parsers are generated at compile time from `EntityGraphQL.g4`. The resulting Abstract Syntax Tree is then processed using the visitor pattern.

# GraphQLVisitor
`GraphQLVisitor` builds on pre-generated visitors from Antlr4.

In `GraphQLVisitor` we visit all the tokens defined in our grammer and process them. Basically we turn them into .Net expressions.

Example, GQL
```gql
query {
    movies {
        id name
    }
    actors { name age }
}
```

The final result of `GraphQLVisitor` is a `GraphQLResultNode` which just holds each top level operation, `GraphQLQueryNode`, `GraphQLMutationNode`, `GraphQLSubscriptionNode` or `GraphQLFragmentNode`.

# GraphQLFragmentNode
`GraphQLFragmentNode` represents a defined fragment - the type it is on and the fields it selects.

# GraphQLMutationNode
`GraphQLMutationNode` wraps up calling the mutation with the arguments and wraps any result selection (the thing in `{}` after your mutation call and arguments) in a `GraphQLQueryNode`. Before executing the resulting selection it does some expression manipulation to have a better formed query for ORMs.

# GraphQLQueryNode
Apart from `GraphQLVisitor`, `GraphQLQueryNode` this is where a majority of the magic happens. It is responsible for putting all the field expressions together. It builds the `Select()` calls and does other expression manipulations like
- Replaces `ParameterExpression`s with the correct ones
- Replaces any fragment fields with the expanded field selection from the `GraphQLFragmentNode`
- Injects services

# GraphQLSubscriptionNode
Not currently implemented.

# Why 2 Steps?

The reason we have 2 steps (first building each field expression in `GraphQLVisitor` and then putting it all together in `GraphQLQueryNode`) is because `GraphQLVisitor` visits each token in order. This means we may not have access to fragments defined below a query in the GQL document.