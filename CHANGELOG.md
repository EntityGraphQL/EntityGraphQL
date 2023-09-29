# 5.1.0

## Changes
- Upgrade to the latest standard Antlr4 - the parser/tool used for the filter expression strings. Fixing precedence of operators

# 5.0.1

## Fixes
- Fix #314 - Some clean up of the Antlr4 grammer for the filter expressions

# 5.0.0

Make sure to check out the changes 5.0.0-beta1

## Breaking Changes
- Generated schema type name for field sort inputs now include the name of the schema type the field is on to avoid conflicts

## Changes
- `IField.AddExtension` now returns the `IField`
- `UseSort()` field extension now can take a list of default sort fields e.g.
- `Broadcaster` (inbuilt `IObservable<TType>` you can use for subscriptions) now has a `OnUnsubscribe` callback

```cs
schema.ReplaceField("people",
    ctx => ctx.People,
    "Return a list of people. Optional sorted")
    .UseSort(
        new Sort<Person>((person) => person.Height, SortDirection.ASC),
        new Sort<Person>((person) => person.LastName, SortDirection.ASC)
    );
```

- `SchemaBuilderOptions` now has a `OnFieldCreated` callback to make changes to fields as `SchemaBuilder` is building the schema.
- `.contains(string)`, `.startsWith(string)`, `.endsWith(string)` & `.toLower()` / `.toUpper()` string methods now available in the filter argument expression.

## Fixes

- Fix naming of fields extracted from service calls when those field use convert

# 5.0.0-beta1

## Breaking Changes

- `EntityGraphQL.AspNet` now targets `net6.0` and `net7.0`, dropping tagets `netcoreapp3.1` or `net5.0`. You can still use the base `EntityGraphQL` library with older targets.
- Interface `IExposableException` has been removed. Use `SchemaBuilderSchemaOptions.AllowedExceptions` or the new `AllowedExceptionAttribute` to define which exceptions are rendered into the results
- #254 - Previously passing `null` for the `ClaimsPrincipal` in `ExecuteRequest()` would skip any authorization checks. All authorization checks are now done regardless of the `ClaimsPrincipal` value. Meaning `null` will fail if there is fields requiring authorization.
- `IDirectiveProcessor` interface has changed. See upgrade docs for changes
- `SchemaBuilderMethodOptions` removed, see updated properties on `SchemaBuilderOptions` and upgrade docs. This was because you can also now add methods as query fields with `GraphQLFieldAttribute`
- `SchemaBuilderOptions.AutoCreateInputTypes` now defaults to `true`. Meaning in `SchemaBuilder` when adding mutations etc any complex types will be added to the schema if they are not there already.
- The rules for reflection on method parameters have been changed to make them clearer. See the upgrade to 5.0 docs and the mutation docs that cover examples.
- `GraphQLValidator` is no longer magically added to your method fields (mutations/subscriptions). If you wish to use it please register it in your services. There is a new helper method in EntityGraphQL.AspNet `AddGraphQLValidator()`. This means you can implement and register your own implementation.
- `SchemaProvider.ExecuteRequest` & `SchemaProvider.ExecuteRequestAsync` have been renamed to `ExecuteRequestWithContext` & `ExecuteRequestWithContextAsync`. The schema context instance provided will be used for all context references within that query.
- #309 - Introduced new `SchemaProvider.ExecuteRequest` & `SchemaProvider.ExecuteRequestAsync` which take no schema context instance as an argument. The context will be fetched from the provided `ServiceProvider` meaning the lifetime rules are adhered to - e.g. `ServiceLifetime.Transient` is now correctly used. This is the perferred way to execute a query

## Changes

- `EntityGraphQL` (the core library) targets both `netstandard2.1` & `net6`. `netstandard2.1` will be dropped around the time of `net8.0` being released.
- Introduced `GraphQLFieldAttribute` to allow you to rename fields in the schema as well as mark methods as fields in the schema. Method parameters will become field arguments in the same way as mutation methods. See updated docs for more information.
- Argument types used for directvies now read `DescriptionAttribute` and `GraphQLFieldAttribute` to use different field name in the schema and set a description
- Added `GraphQLInputTypeAttribute`. Whereas `GraphQLArgumentsAttribute` flattens the types properties into the schema, `GraphQLInputTypeAttribute` assumes the type is an input type and uses that as the schema argument
- You may implement you own `GraphQLValidator` by implementing (and registering) `IGraphQLValidator`
- Added a `options.BeforeExecuting` callback to allow modification of the expression before execution

## Fixes

- #266 - Fix error calling `AddPossibleType()` when some of the types have already been added to the schema
- `ExecutionOptions` passed into `IApplicationBuilder.UseGraphQLWebSockets()` are now used when executing the queries for the subscription
- #284 - Support generic class types as mutation arguments. `MyClass<OtherClass>` will become `input MyClassOtherClass {}`
- #302 - Fix issue where using service fields with `IQueryable`/`DbContext` fields

# 4.3.0

## Changes

- #285 - Add support for implicit operators when converting types
- Fix how EntityGraphQL evaluates the root level fields that return lists. If the return type of the field is nullable a `null` will be returned if the result is `null`. If the return type is non-nullable an empty `List<T>` will be returned.

```cs
public class UserDbContextNonNullable
{
    // empty list will be returned if UserIds resolves to null. If you use nullable types you can control with the ? operator (to return null)
    [GraphQLNotNull]
    public List<string> UserIds { get; set; }
}
```

## Fixes

- #288 - fix stack overflow in `RuntimeTypeJsonConverter`
- #291 - Fix `ResolveWithService` being called twice due to `Expression.Condition` if the field is a list and at the root level of the Query type

# 4.2.1

## Fixes

- Fix issue selecting repeated fields from an interface and a fragment selection
- Fix issue selecting interface fields from the concrete type in a fragment

# 4.2.0

## Changes

- #281 - Implement allowed exceptions to allow exceptions to come into the error result when running with `IsDevelopment == false` as users not be able to modify the exceptions being thrown in order to have them implement `IExposableException`. Exceptions may be allowed with an exact type match, or allowed including all types that inherit it

## Fixes

- Prevent double SQL (when using against EF) query on base type (4.1 regression)
- #279 - Remove duplicate fields when creating expressions
- #280 - Fix for mutations that return interfaces/unions

# 4.1.2

## Fixes

- Fix issue when using paging extension and aliases in the query e.g. `{ myList { myEdges: edges { .. } } }` previously would fail

# 4.1.1

## Fixes

- #221 - Apply null check on the `ToList` expression built to resolve list expressions
- Better support for service fields at the root query level
- Fix support for service fields that take a complex type (object or enumerable) as arguments
- Fix `UseFilter()` `filter` and `UseSort()` `sort` field arguments were incorrectly being marked as required in schema introspection
- Fix issues with default sort values for `UseSort()` not appearing in the schema as default values
- Fix output of default values in schema for lists and objects

# 4.1.0

## Changes

- #262/#205 - Option to prevent leaking internal exceptions into the 'errors' field on a result.

When running in development (read via `IWebHostEnvironment.IsEnvironment("Development")` or when manually creating `SchemaProvider`), messages of exceptions will not be dumped out into the 'errors' field of a query result, unless they implement the newly created (and empty) interface `IExposableException`.

- #260 - Support default values in C# methods for mutations
- #264 - Versions prior to .NET 7, `System.Text.Json` doesn't support the serialization of polymorphic type hierarchies. EntityGraphQL now registers a `RuntimeTypeJsonConverter` class as part of the `DefaultGraphQLResponseSerializer`

## Fixes

- #264 - Interface/union queries used to require you to query for at least 2 of the subtypes at once.
- Fix issue with service fields that take nullable type fields as arguments

# 4.0.1

## Fixes

- #248 - Make sure directives run on fields that map a list of items to a single item (e.g. `myItem(id: Int!) @include(...) { ... }`)
- #213 - Multiple levels of `TargetInvocationException` will now be unwrapped
- #82 - SchemaBuilder can now handle fields that return `Task<>`. Note that the way that queries expressions are built you may still encounter issues with `async` fields not at the root query level. Please open an issue if you do
- #259 - Fix introspection of nullable/non-nullable lists with nullable/non-nullable items
- #239 - Fix issues rejoining main context from a service field

# 4.0.0

## Fixes

- #243 - support `application/json; charset=utf-8` content type

# 4.0.0-beta2

## Fixes

- Fix issue related to #235 where the multiple fields are the same field with different alias and arguments

## Breaking Changes

- `IFieldExtension.ProcessExpressionSelection` now takes a `ParameterExpression? argumentParam` argument which is the argument parameter used at execution time (if there is one). Relating to #235

# 4.0.0-beta1

## Breaking changes

- `DateTimeOffset` is now added as a default scalar type in schema creation. It is a very common used type in EF contexts and trips up new users. If you previously were adding it as a scalar type you no longer need to. Or if you'd like to add it differently (like map it to Date) you can

```c#
services.AddGraphQLSchema<DemoContext>(options =>
{
    options.PreBuildSchemaFromContext = (schema) =>
    {
        schema.RemoveType<DateTimeOffset>();
        // add it how you want
    };
});
```

- `AddMutationsFrom` and friends now take an optional `SchemaBuilderMethodOptions` options object to configure how mutations are built, following the `SchemaBuilderOptions` used elsewhere. See updated docs. Important defaults:

```c#
bool AutoCreateInputTypes = true; // Any input types seen will be added to the schema
bool AddNonAttributedMethods = false; // GraphQLMutationAttributes are still required by default
bool AutoCreateNewComplexTypes = true; // Return types of mutations will be added to the schema
```

- `SchemaBuilderOptions.IgnoreTypes` Now uses `Type` instead of `string`. It is a `HashSet<Type>` now to avoid confusion ofwhich name to use (full name space or not)
- `ProcessExpressionSelection` used in Field Extentions now takes `Dictionary<IFieldKey, CompiledField>` for the `selectionExpressions` parameter. `IFieldKey` is the field name and the schema type the field belongs too. Helps when dealing with inline fragments/union types where we may have multiple fields with the same name from different types.
- `MutationArgumentsAttribute` renamed to `GraphQLArgumentsAttribute` and is used with subscriptions or mutaiton method arguments

## Changes

- Add support for GraphQL subscriptions - see updated documentation
- Added support for definng Union types in the schema (#107)
- Core library `EntityGraphQL` only targets `netstandard2.1` as we do not need to multi-target and this still supports a large dotnet base (3.1+)

## Fixes

- Fix #206 - If a service field included a binary expression EntityGraphQL would sometimes build an incorrect selection expression
- Fix #214 - The implicit conversion return type of a `ArgumentHelper.Required<>()` field argument is non-null
- Fix #212 - Regression using a static or instance method in a service field
- Fix #223 - Mutations with inline args don't support variables with different name to argument name
- Fix #225 - Mutations with separate (not using `MutationArgumentsAttribute`) parameters fail if called without variables
- Fix #229 - Using `Field.Resolve()` would incorrectly assume the field had a service
- #219 - Handle conversion of variables as lists to a `RequiredField<>` arg of the list type
- #215 - Fix issue using GraphQLValidator if using inline mutation arguments
- #235 - Fix issue where arguments with the same name at the same level in a query would receive the same value

# 3.0.5

## Fixes

- Fix #197 - If a mutation returns `Task<T>` use `T` as the return type
- Fix #204 - Nullable reference types correctly produce a nullable argument in the schema
- If not using `MutationArgumentsAttribute` in mutation methods `autoAddInputTypes` was being ignored

# 3.0.4

## Fixes

- Fix #194 - `HasType()` was not checking type mappings

# 3.0.3

## Fixes

- Prevent creation of invalid GraphQL schemas
  - Exception is thrown if you try to add a field with arrguments to types that do not support arguments - `Enum` & `Input` types
  - Exception is thrown if you try to add fields to a `Scalar` type
  - Exception is thrown on invalid type name
- Schema builder now creates a valid GraphQL type name for generic types
- Enum types are now added to the schema as a typed `SchemaType<T>` instance. Meaning you can fetch them using `schema.Type<MyEnum>()`
- Fix #181 - Schema builder now correctly respects `SchemaBuilderOptions.IgnoreTypes`. Note it compares names against the `Type.FullName`
- Fix #182 - Add missing dotnet scalar type for `char`. By default this is added as a schema scalar type named `Char` so the client can decide how to handle it. Over the wire it will serialize as a string (default for `System.Text.Json`). You can change default scalars and mappings by using the `PreBuildSchemaFromContext` action when adding your schema

```
services.AddGraphQLSchema<TContext>(options => {
  options.PreBuildSchemaFromContext = schema =>
  {
      // remove and/or add scalar types or mappings here. e.g.
      schema.AddScalarType<KeyValuePair<string, string>>("StringKeyValuePair", "Represents a pair of strings");
  };
})
```

- When generating a field name for the field that takes an ID argument if the name generated matches the current field EntityGraphQL will add `ById` to the field name. E.g. a property `List<LinePath> Line { get; set; }` previously would try to add the `line: [LinePath]` field with no arguments and another field named `line(id: ID!): LinePath`. This causes an error. EntityGraphQL will name add `lineById(id: ID!): LinePath`. This is because the singularized version of "line" is "line".
- In built field extensions now check if their extra types have already been added by using `Type` instead of name allowing you to add them with your own descriptions etc.
- Fix mutations without any mutation arguments would incorrectly include the query context as an argument

# 3.0.2

## Fixes

- `InputValue` type was being registered under wrong name #184

# 3.0.1

## Fixes

- Input Types were incorrectly getting field arguments added when using `AutoCreateFieldWithIdArguments`

# 3.0.0

## Breaking changes

- `IDirectiveProcessor` now requires a `List<ExecutableDirectiveLocation> On { get; }` to define where the directive is allowed to be used
- Removed obsolete `ISchemaType.BaseType`. Use `ISchemaType.BaseTypes`
- Cleaned up `SchemaType` constructors - using `GqlTypeEnum` instead of many boolean flags
- Removed obsolete `SchemaProvider.AddInheritedType<TBaseType>`
- Removed the instance parameter from `AddMutationsFrom` and friends. Mutation "controllers" are now always created per request like an asp.net controller. Use DI for any constructor parameters
- Renamed `ISchemaType.AddBaseType` to `ISchemaType.Implements` to align with GraphQL language
  - `ISchemaType.Implements` will throw an exception if you try to implement a non-interface
- Renamed `ISchemaType.AddAllBaseTypes` to `ISchemaType.ImplementAllBaseTypes` to align with GraphQL language
- `Create` & `FromObject` on `SchemaBuilder` now take option classes to configure the create of the schema through reflection
  - `ISchemaType.AddAllFields` also takes the option class to configure it's behaviour
  - `ISchemaType.AddAllFields` default behaviour now auto adds any complex types found really reflection the properties & fields and will add those to the schema
- Added new option when building a schema with `SchemaBuilder.FromObject` - `AutoCreateInterfaceTypes`. Defaults to `false`. If `true` any abstract classes or interfaces on types reflected with be added as Interfaces in the schema. This is useful if you expose lists of entities on a base/interface type.

## Changes

- `ToGraphQLSchemaString` now outputs directives in the schema
- #154 - Dyanmically generated types used in the expressions now include the field name the type is being built for to aid in debugging issues
- #146 - Allow GraphQL mutation arguments as seperate arguments in the method signature. Avoiding the need to create the mutation argument classes. e.g.

```
[GraphQLMutation]
public Person AddPersonSeparateArguments(string name, List<string> names, InputObject nameInput, Gender? gender)
{
  // ...
}

[GraphQLMutation]
public Person AddPersonSingleArgument(InputObject nameInput)
{
  // ...
}
```

Turn into

```
type Mutation {
  addPersonSeparateArguments(name: String, names: [String!], nameInput: InputObject, gender: Gender): Person
  addPersonSingleArgument(nameInput: InputObject): Person
}
```

- #160 - Nested data annotations for validation is now supported
- Main `EntityGraphQL` package now targets `net6.0;net5.0;netstandard2.1`
- #170 - EntityGraphQL now replaces query context expressions in service fields by matching the expression instance it extracted. This allows for more complex expressions when passing data to a service field
- Added support for [@oneOf Input Types](https://github.com/graphql/graphql-spec/pull/825). Mark an input type with `GraphQLOneOfAttribute` and EntityGraphQL will mark the type with `@oneOf` in the schema and validate the input meets the requiements on execution

## Fixes

- #171 inheritance support for nested properties / conditional fields
- #176 - allow fully qualified enums in the filter query language

# 2.3.2

## Fixes

- Fix #159 - SchemaBuilder will no longer try to create schema fields for `const` fields on mutation args or input types

# 2.3.1

## Fixes

- #163 - Fix to handle null property in a nested object when processing a System.Text.Json deserialised query document
- #164 - Fix to support inline fragments in a fragment
- #166 - Add missing `!=` operator in the fitler expression language and make sure precedence is correct for logic operators

# 2.3.0

## Changes

- `AddMutationsFrom` now can use the `ServiceProvider` instance to create the mutation class allowing dependency injection at the constructor level like Controllers.
- You can still provide an instance of the mutation class that will be used instead which is the same behaviour as previous, however this method is considered obsolete and will be removed in a future version. We suggest you utilse the ServiceProvider to register your mutation classes with your desired lifetime.
- Allow types to inherit from multiple base classes/interfaces
- Cleanup SchemaType to use an enum instead of lots of boolean type variables. Previous constructor is obsolete
- Cleanup Interfaces api - added a `AddAllBaseTypes`, `AddBaseType` and `AddBaseType(string)` which provides a lot more flexiblity. See updated docs
- Added support for Inline Fragments for types that have interfaces
- `ToGraphQLSchemaString` now orders types and fields by name for consistency regardless of order of fields added and to reduce differences when diffing the schema

## Fixes

- Fix #120 - Error when using `schema.RemoveTypeAndAllFields` and a field of the removing type had a type that has not been added to the schema
- Fix #143 - Error building a null check expression in certain cases.

# 2.2.0

## Changes

- `FromObject` / default schema generator now adds single fields within non root-level fields. E.g. if a root-level field is a list of `people` and each person has a list of `projects` (and projects has an id) is will create a field on `project(id)` field on person
- Add support for nullable reference types - meaning the correct GraphQL schema nullable definitions are generated. @bzbetty

## Fixes

- Fix generation of singluar field with `id` arguments on list fields that use a paging extension when generating a schema. @bzbetty
- Fix - when adding Mutation argument types only search for public instance properties. @breyed
- Fix interface query introspection. @bzbetty
- Fix #137 - multiple line endings were used in the schema output
- Fix - make sure we use an array for arguemnts when expected instead of a list<>

# 2.1.5

- Fix null exception in service field from an object field

# 2.1.4

- Fix #123 do not output types with no fields in the schema definition
- Fix issue calling `ToList` in an expression that then used a `IQueryable` `FirstOrDefault` or friends. Resulting in an invalid expression at runtime

# 2.1.3

- Fix - Throw an error if your query document defines a non-null variable and a null value is supplied
- Service fields that return a list of items are now wrapped in a null check expression like service fields that return a single object are
- Fix - Object fields not included in the schema could be returned when the selection set was missing from the query. Now missing selection sets on list or object fields in queries throw an error. Selection sets are required in graphql queries for non scalar/enum field types. Thanks @breyed

# 2.1.2

- Fix - issue with a field using a static member e.g. DateTime.MaxValue

# 2.1.1

- Fix - issue where a nullable value was incorrectly being called

# 2.1.0

- Added support for interfaces - thanks @bzbetty
- Added `EntityGraphQLException` - use this to throw exceptions and add more error data to the error result via extensions
- `GraphQLValidator` now supports adding custom data via the extensions field
- `ResolveWithService` methods will throw an exception if the expression returns `Task` as we need the result. Use `.GetAwaiter().GetResult()`. Expression do not support `async`/`await` as it is a compiler feature and we need the actual result to build the expression and return the data
- Fix using QueryFilter with an empty string treats it as no value - i.e. no filter
- Fix - the type of an array variable defined as required `[Type!]!` was incorrectly reflected
- Fix - service type's field used in a field's expression was not being extracted into the select. E.g. service field `User.project` has an expression requiring the `user.id` which is not included in the GraphQL query.

# 2.0.3

- Fix thread safety issue with cached queries
- Fix support for using arrays in field argument definitions

# 2.0.2

- fix regression where non-required (nullable) document variables would be set their non-nullable default value. E.g. `Guid`

# 2.0.1

- Fix regression where non service fields in service fields were not being pulled forward to the stage one expression execution.
- Fix regression where services were not be passed to a mutation with no arguments

# 2.0.0

## Breaking changes

- Interface for Field Extensions now are passed a flag telling the extension if this is pre or post the call with service fields
- `GetExpression` in Field Extensions is passed the parent IGraphQLNode - useful when your extension changed the original shape of the object graph, like the paging extensions
- `services.AddGraphQLSchema` adopts a more ASP.NET style `options` callback overload to configure the creation of the schema
- `MapGraphQL` implementation now returns `400` Bad Request status code if the query results contains errors, as a bad query was sent
- Directive interface for building custom directives has changed
- `UseSort` field extension now takes an array of `SortInput<T>` so order of sorts is used
- Parsing floats/doubles/decimals now uses `CultureInfo.InvariantCulture`
  Clean up on the schema building APIs to make them more consistent, documented and concise
- Fix #89 - Remove JSON.NET dependency - This means internally if EntityGraphQL hits a `JObject` or `JToken` it does not know what to do with them. Make sure `QueryRequest.Variables` are fully deserialized. I.e. do not have any `JObject`/`JToken`s in there. Deserialize them to nested `Dictionary<string, object>`.

If you use the EntityGraphQL.AspNet package and the `MapGraphQL()` method you do not need to worry about anything. EntityGraphQL.AspNet uses `System.Text.Json` and handles the nested `JsonElement`s with a custom type converter.

If you are directly using `SchemaProvider.ExecuteRequest()` (i.e. from a Controller or elsewhere), and you are using `Newtonsoft.Json` to deserilaize the incoming `QueryRequest` you can add a custom converter to your schema to handle nested `JObject`/`JToken`s when encounted in query variables.

```
schema.AddCustomTypeConverter(new JObjectTypeConverter());
schema.AddCustomTypeConverter(new JTokenTypeConverter());
```

See the [serialization tests for an example](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/src/tests/EntityGraphQL.Tests/SerializationTests.cs).

- Remove the `WithService()` method used inside a field expression and replace it with `ResolveWithService<TService>()` on the field for easier discovery in your IDE. Example

```
schema.Type<Person>().AddField("age", "A persons age")
  .ResolveWithService<AgeService>(
      (person, ageService) => ageService.GetAge(person.Birthday)
  );
```

- Clean up of `SchemaType` APIs to add/replace/remove fields.
  - Remove `SchemaProvider.Add/ReplaceField` methods.
    - Use `SchemaProvider.Query().Add/ReplaceField()` or `SchemaProvider.UpdateQuery(queryType => {})` to make changes to the root Query type in the schema
  - Additions to the `Field` API to add more uncommon functionality to chaining methods
  - Remove `SchemaProvider.UpdateQueryType()`, use `SchemaProvider.UpdateQuery(type => {})`
  - Remove `SchemaProvider.TypeHasField()`
  - Remove `SchemaProvider.GetQueryFields()` - use `SchemaProvider.Query.GetFields()`
  - Renamed `GetGraphQLSchema()` to `ToGraphQLSchemaString()`
  - Renamed `AddMutationFrom()` to `AddMutationsFrom()`
  - Removed Obsolete methods:
    - `RequiresAllClaims` replaced by `RequiresAllRoles`
    - `RequiresAnyClaim` replaced by `RequiresAnyRole`
    - `ExecuteQuery` replaced by `ExecuteRequest`
    - `ExecuteQueryAsync` replaced by `ExecuteRequestAsync`

## Changes

- Support for persisted queries (enabled by default) - https://www.apollographql.com/docs/react/api/link/persisted-queries/
- Support for a query cache of recent queries. Enabled by default. Caches the result of compiling the query document string to an AST. Execution is then applying the document level variables, building the expressions then execution
- Better support for nested objects in `QueryVariables`
- Performance enhancements when building internal types for arguments
- Performance of compiling and building expressions has been improved - it is about 2 times faster. Note this is just building the expressions, not executing them which triggers your services/EF/etc
- Reduction in memory allocation when compiling queries by around 30%
- You can no add/define mutation methods as delegates/anonymous functions etc.
- You can now use `[Range]` & `[StringLength]` attributes on your arguments for more validation options
- Introduced custom argument validators - a simple way to act on field arguments before execution. Great for custom/complex input validation on arguments. Use `field.AddValidator()` or the `ArgumentValidatorAttribute` attribute.
- Using `AddAllFields(true)` on an `InputType` will add the sub-complex class types as `InputType`s as well
- New option to auto add other classes found on mutation arguments as `InputType`s when adding a mutation

## Fixes

- Fix - Paging field extensions are now thread safe to support multiple different queries being run on the same field at the same time
- Fix #101 - allow custom de/serialization of incoming requests and outgoing responses, via. services `IGraphQLRequestDeserializer` & `IGraphQLResponseSerializer`.
  - _Note that the objects created in the resulting `QueryResult` have fields named like the fields in the schema which is controlled by the `fieldNamer` function provided to the `SchemaProvider` which defaults to GraphQL "standard" (fields camelCase, types PascalCase)_
- Fix field name looks up that were not using the `fieldNamer` function in `SchemaProvider`
- Fix bug where compiler would loop through all available arguments even if it already found the matching type
- Fix argument types of unsigned short/int/long
- Fix #72 - Handling dictionaries introspection - note it will try to create a scalar type `KeyValuePair<T1, T2>` in the schema by default
- Fix handling argument types of unsigned short/int/long
- Fix issue parsing float/double argument values
- Fix null exception with mutations returning a selection with a list field
- Fix directives on mutation fields
- Fix #110 - Field using the wrong sort input object when the fields are the same

# 1.2.1

- Fix #99 issue using the MapGraphQL() extension method

# 1.2.0

- New option to disable introspection. Use `introspectionEnabled` parameter when creating `SchemaProvider`. Defaults to `true`.

# 1.1.2

- Fix #96 - processing int/long/short as list arguments in mutations - thanks @pierrebelin

# 1.1.1

- Fix #92 - error processing mutation arguments that are lists/arrays

# 1.1.0

- Authorization now supports using ASP.NET policies from the `EntityGraphQL.AspNet` package
  - `IAuthorizationService` is resolved to check authorization and needs to be registered
  - `[Authorize]` attribute can be used instead of the `[GraphQLAuthorize]` attribute when using `EntityGraphQL.AspNet`
- Fix - `RequiredAttribute` results in the field being not null in the generated GraphQL Schema
- Fix - Schema introspection was incorrectly returning `__typename` for Enums
- `UseConnectionPagingAttribute` takes optional arguments for default page size and max page size
- `UseOffsetPagingAttribute` takes optional arguments for default page size and max page size
- `EntityGraphQL.AspNet` now uses System.Text.Json for de/serialization internally. It still supports JSON.NET objects in the variable object (as well as System.Text.Json types)
- Fix #19 - you can mark fields (including mutations) as deprecated using the dotnet `[Obsolete]` attribute or the `IField.Deprecate(reason)` method in the schema building
- Fix #88 - If an argument is marked as required and not provided in the query request an error is raised. Mark arguments optional via the API, or make it a nullable type or give it a default value
- Query used in ConnectionPaging now resolves itself using `.ToList()`

## Obsolete - will be removed 2.x

- `RequireAnyClaim()` & `RequireAllClaims()`. Use `RequireAnyRole()` & `RequireAllRoles()` as the `ClaimTypes.Role` was used previously and this change makes it explicit
- `schema.ExecuteQueryAsync/ExecuteQuery(QueryRequest, TContextType, IServiceProvider, ClaimsIdentity, ExecutionOptions)`. Use the `ExecuteRequest`/`ExecuteRequestAsync` methods that take the full `ClaimsPrincipal` as we now support authorization with policies

# 1.0.3

- Fix #86. Mutations not correctly checking required authorization on the mutation field directly

# 1.0.2

- Fix - `RequiredAttribute` results in the field being not null in the generated GraphQL Schema
- Fix issue with expressions failing in a high throughput, threaded use case (heavily hit API)

# 1.0.1

- Fix issue passing optional enum arguments

# 1.0.0

- New extension methods to ease adding your schema to the service collection. See docs - `services.AddGraphQLSchema<DemoContext>(options => {})`
- New package EntityGraphQL.AspNet with extensions to easily expose a graphql endpoint with `MapGraphQL<T>()`.

```c#
app.UseEndpoints(endpoints =>
{
    endpoints.MapGraphQL<DemoContext>();
});
```

- Fix issue reconnecting from a service field to the context
- Fix issue when execution is split across non service fields and then with service fields and the first result is null
- Fix issue using Connection/Offset paging on collection fields that were not on the query root
- Fix issue using Connection/Offset paging on collection fields that have service fields
- Option to add default sort when using `UseSort()` field extension

# 0.70.0

- Introduction of fields extensions to encapsulate common field logic and apply it to many fields. See update [docs](https://entitygraphql.github.io). New built in field extensions
  - `UseConnectionPaging()` which when applied to a collection field modifies the field to implement the GraphQL Connection spec for paging data with metadata
  - `UseOffsetPaging()` which when applied to a collection field modifies the field to implement an offset style paging structure
  - `UseFilter()` which when applied to a collection adds a `filter` argument that takes an expression
  - `UseSort()` which when applied to a collection adds a `sort` arguments that takes fields to sort the collection by
- Replaced Antlr based GraphQL query lexer/parser with HotChocolate.Language Parser. Parsing of query documents is _much_ faster!
- You can now directly use lists (`[12,32]`, etc) and objects (`{name: "Frank"}`) as arguments in the query document. Although it is still recommended to use the `Variables` in the query
- Added benchmarks to help explore performance issues in expression building (What we do with expressions is fast, but found the Antlr Parser was slow)
- Directives now supported on fragment spreads

_Breaking changes_

- EntityGraphQL now targets netstandard2.1
- Some optional arguments in `ExecuteQuery` moved to an Options object

# 0.69.0

- New docs https://entitygraphql.github.io/introduction
- Added `SchemaProvider.UpdateType<T>(Action<SchemaType<T>> updateFunc)` to better help "contain" schema types. Instead of

```
schema.Type<MyType>().AddField(...);
schema.Type<MyType>().AddField(...);
schema.Type<MyType>().AddField(...);
```

we have

```
schema.UpdateType<MyType>(t => {
  t.AddField(...);
  t.AddField(...);
  t.AddField(...);
});
```

Similar for `SchemaProvider.AddType<T>(string name, string description, Action<SchemaType<T>> updateFunc)`

- You can pass a `ILogger<SchemaProvider<T>>` when creating a `SchemaProvider`. Exceptions etc. will be logged
- Added `Take<TSource>(this IQueryable<TSource> source, int? count)` that works on `IQueryable<T>` to support EF translation

_Breaking changes_

- EntityGraphQL now targets netstandard2.0
- Big refactor/clean - hopefully easier to follow the post Antlr (compiled graphql) output - see `GraphQL*Field` classes
- Support for dotnet Entity Framework Core 3.1+ when using other services in the schema (`WithService()`)
- Removed the `Where<TSource>(this IEnumerable<TSource> source, EntityQueryType<TSource> filter)` helper. Use the `WhereWhen` methods that support `EntityQueryType`

To support EF 3.x as a base schema context we now build and execute expressions in 2 stages. See the updated readme section How EntityGraphQL handles WithService().

# 0.68.1

- Update Humanizer.Core dependency which resolves issue with newer dotnet core

# 0.68.0

- Fix issue where `FieldNamer` was not being consistently used. Thanks @AnderssonPeter
- Make sure we include inner exceptions on errors. Thanks @AnderssonPeter
- Added string and long parsing for DateTime and DateTimeOffset. Thanks @GravlLift

# 0.67.0

- As per GraphQL spec commas are optional (previously EntityGraphQL expected them in field/mutation arguments)

_Breaking changes_

- errors property on query result should not be present on the response if there are no errors per the graphQL specification.

# 0.66.1

- Fix bug with using `WithService()` when you require the schema context service again to create a link between services

# 0.66.0

- When using services other than the schema context in fields (that return a single object not a Enumerable) the methods/services are no longer executed multiple times. (issue #36). Notes below
- When a string matches a date time it will be converted to a `DateTime` object. Useful when using the `ArgumentHelper.EntityQuery` for advanced filtering. Regex matches `"yyyy-MM-dd HH:mm:ss.fffffffzzz"`, `"yyyy-MM-dd HH:mm:ss"`, `"yyyy-MM-dd"` with the separator between date and time being either ` ` or `T`
- `EntityQueryCompiler` (used in `ArgumentHelper.EntityQuery`) supports Enums
- `fieldNamer` used in mutations too

_Breaking changes_

- Cleaning up the API. The optional `isNullable` argument is removed from the `AddField()` methods. Use `IsNullable(bool)` method on the `Field` class or the `[GraphQLNotNull]` attribute.
- Cleaning up the API. `fieldNamer` argument removed from methods in `SchemaProvider` and `SchemaType`. Pass in a `fieldNamer` func to the constructor of `SchemaProvider` which will be used when it is auto creating fields. If you pass it in via `SchemaBuilder.FromObject` it will set it on the `SchemaProvider` created.
- `AddCustomScalarType()` removed. Previously marked as obsolete. Use `AddScalarType()`

## Notes of services fix

If you build a field like so

```c#
schema.AddField("myField", ctx => WithService((IMyService srv) => srv.DoSomething(ctx)));

// Register the service with DI somewhere
public class MyService: IMyService {
  public SomeObject DoSomething(Context ctx)
  {
    // do something
    return data;
  }
}
```

With a query like

```gql
{
  myField { field1 field 2 }
}
```

We use to build an expression like so

```c#
srv.DoSomething(ctx) == null ? null : new {
  field1 = srv.DoSomething(ctx).field1,
  field2 = srv.DoSomething(ctx).field2
}
```

We now wrap this in a method call that only calls `DoSomething(ctx)` a single time Which looks like this

```c#
(ctx, srv) => NullCheckWrapper(srv.DoSomething(ctx), parameterValues, selection); // simplifying here

// Again a simified example of what NullCheckWrapper does
public object NullCheckWrapper(Expression<Func<Context, IMyService>> baseValue, object[] values, LambdaExpression selection)
{
  // null check
  if (baseValue == null)
    return null;
  // build the select on the object
  var result = selection.Compile().DynamicInvoke(baseValue);
}
```

This works with services used deeper inthe graph too. Example

```c#
schema.Type<Person>().AddField("complexField", (person) => DoSomething(person.Id));
```

GraphQL

```gql
{
  people {
    complexField {
      field1
      field1
    }
  }
}
```

The wrapped expression looks like this

```c#
(ctx, srv) => ctx.People.Select(person => new {
  complexField = NullCheckWrapper(srv.DoSomething(person.Id), parameterValues, selection); // simplifying here
})
```

This has been tested with EF Core and works well.

# 0.65.0

- You can now secure whole types in the schema. Add the `[GraphQLAuthorize("claim-name")]` to the class or use `schema.AddType(...).RequiresAllClaims("some-claim")`, `schema.AddType(...).RequiresAnyClaim("some-claim")`
- Add `GetField(Expression<Func<TBaseType, object>>)` overload
- operation name is optional for a `query` operation as per GraphQL spec if it is the only operation in the request
- Breaking - removed the `authorizeClaims` argument from `AddField()`. Please use `field.RequiresAllClaims("some-claim")`, `field.RequiresAnyClaim("some-claim")`

# 0.64.0

- Change - descriptions generated for a `.graphql` schema file now use the multiple line triple-quote `"""`
- Fix issue where an `WithService()` expression is wrapped in a `UnaryExpression` and we fail to get the lambda

# 0.63.0

- Expose a `SchemaProvider.ExecuteQueryAsync()`
- Fix #53 support mutations with no arguments
- With the above fix the context and/or the mutation arguments parameters are optional in your mutation method
- the parameters in the mutation methods are no longer required to follow a position
- `SchemaProvider.AddCustomScalarType()` is deprecated, use `AddScalarType`
- Directvies are now included in schema introspection
- Fix #52 - sometimes incorrect types generated for schema intropection or the GraphQL schema file format
- Refactor type information held in the schema. This mean return types etc are evaluated at schema creation time not execution. If you add a field that requires a type as an Arg ument or return type, that type must already be in the schema
- You can now provide a field namer function to name the generated fields when using `SchemaBuilder.FromObject()`, `ISchemaType.AddAllFields()` or `SchemaProvider.PopulateFromContext()`

_Breaking changes_

- The class that represents the mutation arguments must be marked with the `MutationArgumentsAttribute` either at the class level or the parameter
- `SchemaProvider` now adds a default `Date` scalar type in the schema that maps to/from the C# `DateTime` class. If you were previously adding that you'll get an error on type existing. Use `SchemaProvider.RemoveType<DateTime>()` to remove it and add it with a different name
- Type mapping information (`AddTypeMapping()`) are evaluated at schema creation time. You may need to add mappings before creating the rest of your schema

## 0.63.0-beta1 to 0.63.0-beta2

- Removed the empty `IMutationArguments` in favor for a `MutationArgumentsAttribute` on the parameter or the class

# 0.62.0

- Support async mutation methods

# 0.61.0

- Add model validation for mutation arguments. See updated readme
- Fix issue with services not correctly being included when the field is used in a fragment

# 0.60.0

- Add support for directives. Supported by default are `@include(if: Boolean!)` and `@skip(if: Boolean!)`. You can add your own that make changes to the expression pre-execution
- Added syntax support for subscription queries (they compile/no error but do not execute or work)
- Removed support for older syntax of complex queries that is not GQL standard
- Refactored `GraphQLVistor` and friends to make it easier to follow what is happening (I hope). See CONTRIBUTING.md for some notes
- From my testing the compiling and expression building is 15-20% faster than before (still network and or the DB calls are the largest)
- Allow `enum` values in the query schema (e.g. as an argument)
- Ignore static properties & fields on the object passed to `SchemaBuilder.FromObject` - they were not supported and threw errors anyway

# 0.50.1

- Name all `ParameterExpression`s as EF 3.1 expects a name (can throw an error)

# 0.50.0

- Fix claims check when required claims are empty
- Fix error message state which claims are required for the given access error

# 0.50.0-beta1

- Sorry about the quick turn around
- Breaking changes
  - Using DI inspired design now instead of the `TArg`
  - Use `WithService` helper to let EntityGraphQL know which services you require e.g. in an `AddField` use `(db, p) => WithService<IUserProvider>(users => users.Load(p.id))`
  - For mutations just add the services as arguments (at the end) as you typically do in dotnet
  - `schema.ExecuteQuery()` takes an `IServiceProvider` which it uses to look up required services like `IUserProvider` above

# 0.40.0

- Breaking changes
  - Trying to clean up the interface for careting a schema and allowing an easier way to get services in mutations and field selections
  - Rename `MappedSchemaProvider` to `SchemaProvider`
  - Remove extension `object.QueryObject()` and require a more specific call to `SchmeaProvider.ExecuteQuery()`
  - `SchmeaProvider.ExecuteQuery()` takes a `TArg` type which is an argument that will be passed to all field selections and mutation methods. This replaces `mutationArgs` in `object.QueryObject()` and lets both mutations and field selections access other services non-statically (e.g. via `TArg` being an `IServiceProvider`). See updated readme and demo project
  - `SchemaProvider` is now create using `SchemaBuilder.Create<TContext, TArg>()` or `SchemaBuilder.Create<TContext>()` giving you a short cut for not providing a `TArg`
  - `SchemaBuilder.FromObject<TContext, TArg>()` now takes second type argument for the `TArg` value. Also `SchemaBuilder.FromObject<TContext>()` shortcut
- Fix bug where EntityQuery arguments were being cached in the schema

# 0.32.1

- Add `ContextId` to the ignored list in `SchemaBuilder` for EF 3.1

# 0.32.0

- Clean up `LinqExtensions`, removing the overloads that take `LambdaExpression`s. Use `.AsQueryable().Where(someExpressionVar)` instead. It works better with EF and other LinqProviders.

# 0.31.0

- Breaking change - Multiple `[GraphQLAuthorize]` mean all polcies are required and supplying multiple in a single `[GraphQLAuthorize]` mean any
- Do not generate an empty mutation type if there are no mutations
- Fix query introspection to output mapped types correctly
- Support multiple queries in a request with the operation name of which one to run - e.g. how GraphiQL handles multiple queries
- Update error messages
- Clean up some APIs - `SchemaType.AddAllFields()`, `Field.RequiresAllClaims()`, `Field.RequiresAnyClaim()`

# 0.30.0

- Initial support for Authorization and Security in the schema - see updated readme and provide feedback

# 0.29.0

- Support `fragment` statements in other positions in the query

# 0.28.2

- Fix error with `EntityQueryType<>` as a field argument not being defined as a `String` in introspection

# 0.28.1

- Fix issue introduced in 0.28 when using the `RequiredField<>` type

# 0.28.0

- Only convert a string matching a `Guid` when the arg type is a `Guid` or `Guid?`

# 0.27.2

- Fix issue where a non-required EntityQueryType Filter throw an error if it wasn't supplied

# 0.27.1

- Better support mutations that return an object, not a list

# 0.27.0

- Introspection query `__type(name: "")` now correctly returns an object not an array
- `[Description("")]` attributes on `enum` fields are now read into the schema
- Fix issue where introspection query would have dupelicate types for enum types

# 0.26.0

- `ISchemaType.AddAllFields` requires a schema as it can add newly discovered types to that schema
- `ISchemaType.AddAllFields` by default adds new `enum` types to the schema if found as a field type
- `ISchemaType.AddAllFields` can (off by default) add new complex types to the schema if found as a field type
- `Schema.Type<TType>()` now searches by `TType` not `typeof(TType).Name`. Allowing you to add a type with a different name but still get the typed `SchemaType<T>` back

# 0.25.0

- Add the ability to add enum types to the schema
- The auto schema builder now adds enum types it finds to the schema by default
- Enum values are referenced by the string value, if using JSON.NET you will want to use `[JsonConverter(typeof(StringEnumConverter))]`

# 0.24.0

- Add `GraphQLNotNullAttribute` to mark fields as not nullable in the graphql schema
- By default when generating a `.schema` file `IEnumerable<T>` will generate the element type as not nullable. E.g. `[T!]`. Use `GraphQLElementTypeNullableAttribute` to mark it that the list can contain null items
- Support mapping `decimal` to `number`
- Better support for defining nullable or non-nullable types

# 0.23.3

- Allow adding a mapping type to a type that already exists in the schema. E.g. you might add an input type `Point` and want to map the dotnet type `Point` to it.

# 0.23.2

- fix issue with required type in an array e.g. `[ID!]`

# 0.23.1

- fix issue with generated required types

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
