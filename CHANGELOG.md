# 0.23.0
- Output fields as required in .graphql schemas

# 0.22.0
- You can now specify if a field is ignored for queries, mutations or both (if you're sharing DTOs/objects)
- Don't output the meta information of the schema in the schema definition
- Prevent duplicate scalar types in schema generation
- Fix issue where `mutation` keyword could not have any white space before it

# 0.21.0
- Change, make ENUMs a `Int` type as dotnet serialises them like that
- Fix input types missing for the SDL schema generated
- Fix issue where mutation args has repeating `ofType` data in introspection
- Fix issue where InputTypes would be duplicated in introspection query results
- Fix issue where `ToList()` was being called deep in expressions causing issues with EF

# 0.20.0
- Add the ability to add custom scalar types to the schema
- Fix a bug where introspection queries were in correct if you have a List/Array in your mutation type

# 0.19.1
- Fix a bug where mutation arg objects retained values from previous mutation - i.e if the next call to that mutation didn't provide some optional arguments

# 0.19.0
- `QueryObject()` calls `ToList()` on any lists so if you are using something like EF all queries will be evaluated on `QueryObject()`, not just the ones that return a single object (as they call `FirstOrDefault()`). This is more consistent, the result contains all your data (regardless of underlying ORM or not) and not a miz of evaluated and non-evaluated.
- Add `RemoveTypeAndAllFields()` on `MappedSchemaProvider` to more easily clean up a schema that was auto created by `SchemaBuilder.FromObject<T>()`

# 0.18.4
- Fix case matching of arguments in mutations. EntityGraphQL defaults to turning dotnet `UpperCaseFieldsAndProperties` to `camelCaseFieldsAndProperties` as this was the intention with the change in 0.18.0
- Enum values are as defined. E.g. if you have an enum `Meter` you can use `"Meter"` not `"meter"`

# 0.18.3
- map Int16 and UInt16 to Int
- Fix issue where argument names were not case sensitive (the Breaking change introduced in 0.18)

# 0.18.2
- Fix `kind` in schema introspection query to not have `!`

# 0.18.1
- Update dependences (JSON.NET)
- Fix some small casing issues

# 0.18.0
- Support for schema introspection. Top two fields (`__schema` and `__type(name: String!)`) are implemented. There are some things missing where we currently don't support the feature (directives). GraphiQL introspection query executes and you can naviagte the example. Big thanks to @JTravis76 for starting the work.
- Implement #18 default argument values in GQL operations `mutation MyMutation($arg: String = "hey defaults") { ... }`
- Breaking change - If you purely use `SchemaBuilder.FromObject()` it now creates all field names `lowerCaseCamel` and type names `UpperCamelCase` like the GraphQL defaults. Also since GraphQL is case sensitive I have enforced that. You _may_ need to update some queries to match casing

# 0.17.0
- Add the option to have other parameters passed into a mutation method. Very useful for IServiceProvider or other services your mutations require

# 0.16.2
- Fix issue where duplicate fields (say from a query and a fragment) would cause an error

# 0.16.1
- Bring inner exception details up
- Fix issue where fields that require arguments fail (See Paging example in Demo)

# 0.16.0
- Add support for full queries on a mutation result. See readme for more details (you return an Expression)
- Initial support for GraphQL fragments. fix #2
- Fix issues with using field arguments deeper in the graph schema
- Add support for `operationName` in the `GraphQLRequest`
- Add support for graphql comments
- Add GraphiQL web interface to the demo app
- Add `GraphQLIgnore` attribute to ignore a field on a Type for the automatic schema builder
- fix bug where it would sometimes try to give an `UInt64` to an argument that required an `Int32`
- (beta) Add `dotnet gql` command to generate a schema from your current DBContext. See readme for more info

# 0.15.8
- Add `WhereWhen()` to the extension helpers

# 0.15.7
- Fix bug in handling converting a string into an enum
- Fix issue looking up types internally when working with anonymous types

# 0.15.6
- Fix issue with incorrectly checking for generic types

# 0.15.5
- Fix issue with array types in the dynamically generated type for lambdas

# 0.15.4
- Fix issue where the generated GraphQL schema had the incorrect type for arrays (e.g. double[])
- Fix issue with `SchemaBuilder.FromObject` building invalid types from arrays.

# 0.15.3
- Fix an issue where it would generate an invalid field name if we couldn't singularize the name

# 0.15.2
- Move the query results into a `QueryResult` class so it is easier to work with

# 0.15.1
- Fix #11 - failure to use `RequiredField` with types like `uint` - it would try to initialise it with an `int`

# 0.15
- Remove old code that supported multiple fields with the same name and different arguments. GrpahQL doesn't support that and it caused a bug where it would sometimes not find the field you want. You can implment optional arguments and build a complex field like `schemaProvider.AddField("myField", new {name = (string)null, id = (Guid?)null}, (db, param) => param.id.HasValue ? db.MyEntities.Where(l => l.Id == param.id).FirstOrDefault() : db.MyEntities.Where(l => l.Name == param.name).FirstOrDefault(), "Returns an Entity object by ID or a match on the name argument");`

# 0.14.4
- Allow user to supply a type mapping for the generated GraphQL schema

# 0.14.3
- Fix an issue where we would some time identify a type as IEnumerable when it shouldn't have been
- Allow `ReplaceField` on a sub type

# 0.14.2
- Convert enums correctly

# 0.14.1
- Fix SchemaGenerator to support arrays better
- Support turning `QueryRequest` variables from JSON objects into their requied objects

# 0.14.0
- Bring back `ReplaceField` as technically GraphQL doesn't support overloading, so if you're using tools like Apollo etc to generate code you need to only have unique fields
- Added `GetGraphQLSchema` on the schema to return a `.graphql` schema file for input into tools like Apollo codegen
- Add the option to add descriptions to Mutations

# 0.13.1
- Fix issue where operation name with no arguments failed

# 0.13.0
- Breaking change: Strings are now defined by double quotes `"` to fit in with GraphQL better
- Added support to use a LINQ-style language as a argument in GraphQL. E.g the following GQL field, `{ users(filter: "active = true and age > 20") { id name } }` can be defined with `schema.AddField("users", new {filter = EntityQuery<User>()}, (ctx, p) => ctx.Users.Where(p.filter), "Return filtered users")`. Letting you write that filter using any fields defined on the `User` object
- Add `null`/`empty` constant for checking for null in EQL

# 0.12.1
- Fix bug converting Int values to double? values

# 0.12.0
- Support field "overloads" - fields with same name and different arguments.

# 0.11.0
- Renamed package EntityGraphQL to emphasizes that it implements GraphQL

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