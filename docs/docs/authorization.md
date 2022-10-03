---
sidebar_position: 5
---

# Authorization

You should secure the route where you app/client posts request to in any ASP.NET supports. Given GraphQL works with a schema you likely want to provide authorization within the schema. EntityGraphQL provides support for checking claims on a `ClaimsPrincipal` object.

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

Authorization can be provided in the API for add/replacing fields on the schema objact.

```cs
schemaProvider.AddField("myField", (db) => db.MyEntities, "Description").RequiresAllRoles("admin");
schemaProvider.AddField("myField", (db) => db.MyEntities, "Description").RequiresAnyRole("admin", "super-admin");

schemaProvider.AddType<Property>("properties", (db) => db.Properties, "Description").RequiresAllRoles("property-user");
schemaProvider.AddType<Property>("properties", (db) => db.Properties, "Description").RequiresAnyRole("property-user", "property-admin");
```

Note when using `AddField()` and `AddType()` these functions will automatically search for `[Authorize()]` attributes on the fields and types.

## Authorization without ASP.Net

You can use the `GraphQLAuthorizeAttribute` with role claims to provide authorization without the ASP.Net dependency.
