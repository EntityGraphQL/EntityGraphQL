# 0.10.1
- Fix issue with arrays and objects in variables parameter on the request
- Better error messages when GraphQL arguments can't be mapped to an object
- Validate required variables in operations are supplied in the variables dictionary
- Fix potential issue with selecting sub object graphs

# 0.10.0
- Change the errors to appear in `errors` key along side `data` key as per GraphQL
- Fix an issue when selecting fields from a mutation result

# 0.9.4
- Support selecting an object that may be null `{ person {name} }` `person` will be null if it does not exist

# 0.9.3
- Parameter types can be optional (no `!`)

# 0.9.2
- Fix type issues with mutation args
- Change auto entity call with `id:` (e.g. `{ entity(id: 13)}`) to call the `First()` method last
- Fix issue with selecting a one-to-one relation in a query

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