---
sidebar_position: 5
---

# Authorization

You should secure the route where you app/client posts request to in any ASP.NET supports. Given GraphQL works with a schema you likely want to provide authorization within the schema. EntityGraphQL provides support for checking claims on a `ClaimsPrincipal` object.

## Authorization Services

EntityGraphQL supports different authorization service implementations:

- **`RoleBasedAuthorization`** - The default. Checks roles on the `ClaimsPrincipal`. Use when you only need role-based authorization.
- **`PolicyOrRoleBasedAuthorization`** - Supports both ASP.NET Core policies and roles. This is the default when calling `AddGraphQLSchema()` in `EntityGraphQL.AspNet` if `IAuthorizationService` is available.

### Configuring Authorization Service

When using `AddGraphQLSchema()` in ASP.NET, `PolicyOrRoleBasedAuthorization` is used by default. To use a different authorization service:

```cs
builder.Services.AddSingleton<IAuthorizationService, MyCustomAuthService>();

// Or use role-based authorization only
builder.Services.AddSingleton<IAuthorizationService, RoleBasedAuthorization>();

builder.Services.AddGraphQLSchema<DemoContext>(options =>
{
    // Configure things
});
```

When creating a schema manually outside of ASP.NET:

```cs
var schema = new SchemaProvider<DemoContext>(
    authorizationService: new MyCustomAuthService()
);
```

## Passing in the User

First pass in the `ClaimsPrincipal` to the query call

_Note if you are using the `AddGraphQLSchema()` extension in `EntityGraphQL.AspNet` this is already handled for you._

```cs
// Assuming you're in a ASP.NET controller
// this.User is the current ClaimsPrincipal
var results = await schemaProvider.ExecuteRequestAsync(query, dbContext, this.HttpContext.RequestServices, this.User);
```

## Adding Authorization on Roles or Policies

You can add authorization requirements throughout your schema even using the `AuthorizeAttribute` or when building/modifying your schema.

\_Note: if you provide multiple `[AuthorizeAttribute]` attributes on a single field/mutation they are treated as `AND` meaning all are required. If you provide a single `[AuthorizeAttribute]` attribute with multiple roles/policies in a comma-separated string they are treated as `OR` i.e. having any of those listed will authorize access.

## Mutations

Mark you mutation methods with the `[Authorize(Roles = "role-name")]` attribute.

Policy authorization with `[Authorize(Policy = "policy-name")]` is also supported when using `EntityGraphQL.AspNet`.

```cs
public class MovieMutations
{
  [GraphQLMutation]
  [Authorize(Roles = "movie-editor")]
  public Movie AddActor(MyDbContext db, ActorArgs args)
  {
    // ...
  }
}
```

If a `ClaimsPrincipal` is provided with the query call it will be required to be Authorized and have a Role of `movie-editor` to call this mutation.

## Queries

If you are using the `SchemaBuilder.FromObject<TContext>()` you can use the `[Authorize(Roles = "role-name")]` attribute again throughout the objects.

```cs
public class MyDbContext : DbContext {
  protected override void OnModelCreating(ModelBuilder builder) {
    // Set up your relations
  }

  // require either claim
  [Authorize(Roles = "property-role,admin-property-role")]
  public DbSet<Property> Properties { get; set; }
  public DbSet<PropertyType> PropertyTypes { get; set; }
  public DbSet<Location> Locations { get; set; }
}

public class Property {
  public uint Id { get; set; }
  public string Name { get; set; }
  public PropertyType Type { get; set; }
  // require both claims
  [Authorize(Roles = "property-admin")]
  [Authorize(Roles = "super-admin")]
  public Location Location { get; set; }
}

// ....
```

You can secure whole types with the attribute too.

```cs
[Authorize(Roles = "property-user")]
public class Property {
  public uint Id { get; set; }
  public string Name { get; set; }
  public PropertyType Type { get; set; }
  public Location Location { get; set; }
}
```

If a `ClaimsPrincipal` is provided with the `ExecuteRequest` call it will be required to be Authorized and have the Role `property-role` to query the root-level `properties` field and the role `property-admin` to query the `Property` field `location`.

Authorization can be provided in the API for add/replacing fields on the schema object.

```cs
schemaProvider.AddField("myField", (db) => db.MyEntities, "Description").RequiresAllRoles("admin");
schemaProvider.AddField("myField", (db) => db.MyEntities, "Description").RequiresAnyRole("admin", "super-admin");

schemaProvider.AddType<Property>("properties", (db) => db.Properties, "Description").RequiresAllRoles("property-user");
schemaProvider.AddType<Property>("properties", (db) => db.Properties, "Description").RequiresAnyRole("property-user", "property-admin");
```

Note when using `AddField()` and `AddType()` these functions will automatically search for `[Authorize()]` attributes on the fields and types.

## Authorization without ASP.Net

You can use the `GraphQLAuthorizeAttribute` with role claims to provide authorization without the ASP.Net dependency.

## Custom Authorization

EntityGraphQL's authorization system is built on a flexible keyed data structure that allows you to extend it with custom authorization requirements.

### How Authorization Works

Authorization requirements are stored in a `RequiredAuthorization` object which uses a keyed data dictionary. This allows different authorization implementations to store their own custom data:

- **Core library** uses `"egql:core:roles"` key for role-based authorization
- **EntityGraphQL.AspNet** uses `"egql:aspnet:policies"` key for policy-based authorization
- **Your custom implementation** can use any namespaced key (e.g., `"myapp:custom-auth"`)

### Creating a Custom Authorization Service

To implement custom authorization, create a class that extends `RoleBasedAuthorization` or implements `IGqlAuthorizationService`:

```cs
public class CustomAuthorizationService : RoleBasedAuthorization
{
    private const string CustomDataKey = "myapp:custom-permissions";

    public override bool IsAuthorized(ClaimsPrincipal? user, RequiredAuthorization? requiredAuthorization)
    {
        if (requiredAuthorization != null && requiredAuthorization.Any())
        {
            // Check your custom authorization data
            // The data is List<List<string>> to support AND/OR combinations
            // Each inner list is OR'd together, outer lists are AND'd
            // Example: [["perm1", "perm2"], ["perm3"]] means (perm1 OR perm2) AND perm3
            if (requiredAuthorization.TryGetData(CustomDataKey, out var permissionGroups))
            {
                foreach (var permissionGroup in permissionGroups)
                {
                    // User must have at least one permission from this group (OR)
                    var hasAnyPermission = permissionGroup.Any(permission =>
                        UserHasPermission(user, permission));

                    if (!hasAnyPermission)
                        return false; // User doesn't have any permission from this group (AND failed)
                }
            }

            // Also check roles
            return base.IsAuthorized(user, requiredAuthorization);
        }
        return true;
    }

    private bool UserHasPermission(ClaimsPrincipal? user, string permission)
    {
        // Your custom permission logic
        return user?.HasClaim("permission", permission) ?? false;
    }
}
```

### Adding Custom Authorization Data

You can add custom authorization requirements using extension methods:

````cs
public static class CustomAuthorizationExtensions
{
    private const string CustomDataKey = "myapp:custom-permissions";

    public static IField RequiresAnyPermission(this IField field, params string[] permissions)
    {
        field.RequiredAuthorization ??= new RequiredAuthorization();

        // Get existing permission groups or create new list
        if (!field.RequiredAuthorization.TryGetData(CustomDataKey, out var permissionGroups))
        {
            permissionGroups = new List<List<string>>();
            field.RequiredAuthorization.SetData(CustomDataKey, permissionGroups);
        }

        // Add as a new group where any permission satisfies (OR within group)
        permissionGroups.Add(permissions.ToList());
        return field;
    }

    public static IField RequiresAllPermissions(this IField field, params string[] permissions)
    {
        field.RequiredAuthorization ??= new RequiredAuthorization();

        if (!field.RequiredAuthorization.TryGetData(CustomDataKey, out var permissionGroups))
        {
            permissionGroups = new List<List<string>>();
            field.RequiredAuthorization.SetData(CustomDataKey, permissionGroups);
        }

        // Add each permission as a separate group (AND across groups)
        foreach (var permission in permissions)
        {
            permissionGroups.Add(new List<string> { permission });
        }
        return field;
    }

    public static SchemaType<TBaseType> RequiresAnyPermission<TBaseType>(
        this SchemaType<TBaseType> schemaType, params string[] permissions)
    {
        schemaType.RequiredAuthorization ??= new RequiredAuthorization();

        if (!schemaType.RequiredAuthorization.TryGetData(CustomDataKey, out var permissionGroups))
// Use in your schema
schemaProvider.AddField("sensitiveData", (db) => db.SensitiveEntities, "Sensitive data")
    .RequiresAnyPermission("read:sensitive-data");

schemaProvider.Type<User>()
    .ReplaceField("salary", u => u.Salary, "User's salary")
    .RequiresAllPermissions("read:salaries", "read:user-data");
``` }

    public static SchemaType<TBaseType> RequiresAllPermissions<TBaseType>(
        this SchemaType<TBaseType> schemaType, params string[] permissions)
    {
        schemaType.RequiredAuthorization ??= new RequiredAuthorization();

        if (!schemaType.RequiredAuthorization.TryGetData(CustomDataKey, out var permissionGroups))
        {
            permissionGroups = new List<List<string>>();
            schemaType.RequiredAuthorization.SetData(CustomDataKey, permissionGroups);
        }

        foreach (var permission in permissions)
        {
            permissionGroups.Add(new List<string> { permission });
        }
        return schemaType;
    }
}
````

### Using Custom Authorization

```cs
// Configure your custom authorization service
services.AddGraphQLSchema<DemoContext>(options => {
    options.Schema.AuthorizationService = new CustomAuthorizationService();
});

// Use in your schema
schemaProvider.AddField("sensitiveData", (db) => db.SensitiveEntities, "Sensitive data")
    .RequiresPermission("read:sensitive-data");

schemaProvider.Type<User>()
    .ReplaceField("salary", u => u.Salary, "User's salary")
    .RequiresPermission("read:salaries");
```

### Combining Multiple Authorization Types

The keyed data structure allows multiple authorization requirements to coexist:

```cs
schemaProvider.AddField("adminData", (db) => db.AdminData, "Admin only data")
    .RequiresAllRoles("admin")                    // Role-based auth
    .RequiresAnyPermission("read:admin")          // Custom permission auth
    .RequiresAllPolicies("AdminPolicy");          // ASP.NET policy auth (requires EntityGraphQL.AspNet)
```

All authorization requirements must be satisfied for access to be granted.
