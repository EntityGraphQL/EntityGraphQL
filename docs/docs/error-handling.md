---
sidebar_position: 7
---

# Error Handling

EntityGraphQL provides comprehensive error handling that follows the GraphQL specification. This page covers all aspects of how errors work in EntityGraphQL, from validation errors to execution errors and partial results.

## Types of Errors

EntityGraphQL handles several types of errors differently:

### 1. Validation Errors

These occur during query parsing and validation, before execution begins:

- **Query syntax errors**: Invalid GraphQL syntax
- **Schema validation**: Fields that don't exist, wrong argument types, etc.
- **Argument validation**: Using `[Required]`, `[Range]`, `[StringLength]` attributes

```csharp
public class PersonArgs
{
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; }

    [Range(0, 150, ErrorMessage = "Age must be between 0 and 150")]
    public int Age { get; set; }
}
```

Validation errors:

- Prevent query execution entirely
- Are returned in the `errors` array with `data: null`

### 2. Execution Errors

These occur during field execution:

```csharp
[GraphQLMutation]
public Person AddPerson(string name)
{
    if (string.IsNullOrEmpty(name))
        throw new ArgumentException("Name cannot be empty");

    // ... rest of logic
}
```

Execution errors:

- Allow partial results (other fields can still succeed)
- Are returned alongside successful data

## Error Response Format

### Validation Error Response

```json
{
  "data": null,
  "errors": [
    {
      "message": "Field 'nonExistentField' does not exist on type 'Query'"
    }
  ]
}
```

### Execution Error Response

```json
{
  "data": {
    "successfulField": "some data",
    "failedField": null
  },
  "errors": [
    {
      "message": "Name cannot be empty",
      "path": ["failedField"]
    }
  ]
}
```

## Partial Results

EntityGraphQL supports **partial results** according to the GraphQL specification. When multiple fields are requested and some fail, you get results from successful fields plus error information.

### How It Works

EntityGraphQL executes each top-level field separately:

```graphql
query MultipleFields {
  users {
    id
    name
  } # Succeeds
  posts {
    id
    title
  } # Fails
  comments {
    id
    text
  } # Succeeds
}
```

**Response:**

```json
{
  "data": {
    "users": [{ "id": "1", "name": "Alice" }],
    "posts": null,
    "comments": [{ "id": "1", "text": "Great post!" }]
  },
  "errors": [
    {
      "message": "Access denied to posts",
      "path": ["posts"]
    }
  ]
}
```

### Nullable vs Non-Nullable Fields

Field nullability affects error behavior:

#### Nullable Fields

```csharp
// This field is nullable
schema.Query().AddField("optionalData", ctx => MightFail());
```

- Failed nullable fields return `null`
- Error included in `errors` array
- Other fields continue executing

#### Non-Nullable Fields

```csharp
// This field is non-nullable
schema.Query().AddField("requiredData", ctx => MightFail()).IsNullable(false);
```

- Failed non-nullable fields bubble up to the next nullable parent
- May cause entire `data` to become `null` if error reaches the root
- Follows GraphQL spec error propagation rules

### Aliases

When using field aliases, the path uses the alias name:

```graphql
query {
  primaryUser: user(id: 1) {
    name
  }
  backupUser: user(id: 2) {
    name
  }
}
```

Error path: `["primaryUser"]` or `["backupUser"]`

## Using IGraphQLValidator

For collecting multiple validation errors in mutations:

```csharp
[GraphQLMutation]
public Person AddPersonWithValidation(PersonInput input, IGraphQLValidator validator)
{
    if (string.IsNullOrEmpty(input.Name))
        validator.AddError("Name is required");

    if (input.Age < 0)
        validator.AddError("Age must be positive");

    if (input.Age > 150)
        validator.AddError("Age seems unrealistic");

    // Check for errors before proceeding
    if (validator.HasErrors)
        return null; // Errors automatically included with field path

    return CreatePerson(input);
}
```

**Register the validator:**

```csharp
services.AddGraphQLValidator(); // In ASP.NET Core
```

This returns multiple errors for a single field:

```json
{
  "data": { "addPersonWithValidation": null },
  "errors": [
    {
      "message": "Name is required",
      "path": ["addPersonWithValidation"]
    },
    {
      "message": "Age must be positive",
      "path": ["addPersonWithValidation"]
    }
  ]
}
```

## Exception Handling

### Development vs Production

EntityGraphQL gates exception-message disclosure behind an `IsDevelopment` flag. In **production mode** (the default for ASP.NET deployments), any exception not on the allow-list is replaced with the generic message `"Error occurred"`:

```json
{
  "message": "Field 'reports' - Error occurred"
}
```

In **development mode**, the raw message is forwarded as-is:

```json
{
  "message": "Field 'reports' - Object reference not set to an instance of an object"
}
```

#### How `IsDevelopment` is set

**ASP.NET (recommended)** — `AddGraphQLSchema` auto-detects `IWebHostEnvironment`. When the environment is `"Development"`, messages are disclosed; every other named environment (Staging, Production, etc.) masks them. You never need to set this manually in typical setups.

To override the auto-detection — for example to enable development messages in a staging environment during an incident — set `IsDevelopment` explicitly:

```cs
builder.Services.AddGraphQLSchema<MyContext>(options =>
{
    options.Schema.IsDevelopment = true; // force dev messages even in non-Development env
});
```

**Direct schema creation** (non-ASP.NET) — `SchemaProviderOptions.IsDevelopment` defaults to `true`, so messages are disclosed unless you opt out:

```cs
var schema = SchemaBuilder.FromObject<MyContext>(
    schemaOptions: new SchemaProviderOptions { IsDevelopment = false }
);
```

### Allowed Exceptions

Even in production mode, certain exceptions can be allowed to pass their message through. EntityGraphQL's own exception types (`EntityGraphQLException`, `EntityGraphQLFieldException`, `EntityGraphQLSchemaException`) are always allowed.

For your own domain exceptions, use `[AllowedException]` on the class or add them to the allow-list at schema setup:

```csharp
// Mark your exception class directly
[AllowedException]
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

// Or add them at schema creation
builder.Services.AddGraphQLSchema<MyContext>(options =>
{
    options.Schema.AllowedExceptions.Add(new AllowedException(typeof(ArgumentException)));
    // Pass exactMatch: true to require the exact type (no subclasses)
    options.Schema.AllowedExceptions.Add(new AllowedException(typeof(DomainException), exactMatch: true));
});
```

## Custom Error Extensions

Add custom data to errors:

```csharp
[GraphQLMutation]
public Person AddPerson(PersonInput input, IGraphQLValidator validator)
{
    if (input.Age < 18)
    {
        validator.AddError("Age restriction", new Dictionary<string, object>
        {
            ["code"] = "AGE_RESTRICTION",
            ["minimumAge"] = 18,
            ["providedAge"] = input.Age
        });
        return null;
    }

    return CreatePerson(input);
}
```

**Response:**

```json
{
  "errors": [
    {
      "message": "Age restriction",
      "path": ["addPerson"],
      "extensions": {
        "code": "AGE_RESTRICTION",
        "minimumAge": 18,
        "providedAge": 15
      }
    }
  ]
}
```
