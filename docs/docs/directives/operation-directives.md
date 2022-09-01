---
sidebar_position: 1
---

# Operation Directives

[Operation Directives](https://graphql.org/learn/queries/#directives) provide a way to dynamically change the structure and shape of our queries using variables. An example from the GraphQL website:

```graphql
query Hero($episode: Episode, $withFriends: Boolean!) {
  hero(episode: $episode) {
    name
    friends @include(if: $withFriends) {
      name
    }
  }
}
```

Now if you set `withFriends` to `true` in the variables passed with the query POST you'll get the `friends` result. If you set it to `false` you will not. Thus dynamically changing the shape of your query.

## Built-In Directives

The GraphQL spec defines 2 directives that are supported out of the box in EntityGraphQL.

- `@include(if: Boolean)` - Only include this field in the result if the argument is true.
- `@skip(if: Boolean)` - Skip this field if the argument is true.

## Custom Directives

See the [IncludeDirective](https://github.com/lukemurray/EntityGraphQL/blob/master/src/EntityGraphQL/Directives/IncludeDirectiveProcessor.cs) implementation to see how you could implement a custom directive. You can add your directive to the schema with the following

```cs
// Example only, you don't need to actually add Include or Skip directives
schema.AddDirective(new IncludeDirective());
```

These directives work on the internal representation of a field `GraphQLQueryNode`. This is working against the query graph not the data result.

_There is more functionality planned for custom directives which can work on both the pre-execution query graph or the post-execution data._