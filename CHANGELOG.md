# 0.8.0
- `Moved EntityQueryLanguage.DataApi` namespace to `EntityQueryLanguage.GraphQL` to better communicate that its intent is to adds\ GraphQL support
- Add support the GraphQL arguments. See updated readme
- Move data results to field `data` in the resulting object to match GraphQL
- Support the `query` keyword for graphql

# 0.7.0
- Added support for complex nested queries when using the `EFRelationshipHandler`
- Fix type caching issue causing exception on changing a sub query