Yes please :)

Check out any open issues, or dive in to add some functionality. Below I'll try to outline a few interesting parts of the code base.

# GraphQLCompiler
`GraphQLCompiler` just uses the generated parsers from Antlr4 to parse the GraphQL string. The parsers are generated at compile time from `EntityGraphQL.g4`. The resulting Abstract Syntax Tree is then processed using the visitor pattern.

# GraphQLVisitor
`GraphQLVisitor` builds on pre-generated visitors from Antlr4.

In `GraphQLVisitor` we visit all the tokens defined in our grammer ans process them. Basically we turn them into .Net expressions.

Example, GQL
```
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
Apart from `GraphQLVisitor`, `GraphQLQueryNode` is where a majority of the magic happens. It is responsible for putting all the field expression together. It builds the `Select()` calls and does other expression manipulations like
- Replaces `ExpressionParameter`s with the correct ones
- Replaces any fragment fields with the expanded field selection from the `GraphQLFragmentNode`
- Injects services

# GraphQLSubscriptionNode
Not currently implemented.

The reason we build the final expression here is it is after the completion of the visitor and we no have access to fragments that may have been define after we first see them used.