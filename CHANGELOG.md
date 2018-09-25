# 0.9.1
- Fix a issue where schema and metrhod provider were not being passed down in `QueryObject`

# 0.9.0
- Add initial support for Mutations
- `,` between fields is optional like in GraphQL
- Support `__typename` metadata

# 0.8.0
- `Moved EntityQueryLanguage.DataApi` namespace to `EntityQueryLanguage.GraphQL` to better communicate that its intent is to support
- Add support GraphQL arguments in fields. See updated readme
- By default `SchemaBuilder.FromObject<TType>()` generates a non-pural field for any type with a public `Id` property, with the argument name of `id`. E.g. A field `people` that returns a `IEnumerable<Person>` will result in a `person(id)` field
- Move results to field `data` in the resulting object to match GraphQL
- Support the `query` keyword for graphql

# 0.7.0
- Added support for complex nested queries when using the `EFRelationshipHandler`
- Fix type caching issue causing exception on changing a sub query