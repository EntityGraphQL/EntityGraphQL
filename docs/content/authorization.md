---
title: "Authorization"
metaTitle: "Adding authorization to your schema - EntityGraphQL"
metaDescription: "Adding authorization to your GraphQL schema"
---

You should secure the route where you app/client posts request to in any ASP.NET supports. Given GraphQL works with a schema you likely want to provide security within the schema. EntityGraphQL provides support for checking claims on a `ClaimsIdentity` object.

First pass in the `ClaimsIdentity` to the query call

```
// Assuming you're in a ASP.NET controller
var results = _schemaProvider.ExecuteQuery(query, _dbContext, HttpContext.RequestServices, this.User.Identities.FirstOrDefault());
```

Now if a field or mutation has `AuthorizeClaims` it will check if the supplied `ClaimsIdentity` contains any of those claims using the claim type `ClaimTypes.Role`.

_Note: if you provide multiple `[GraphQLAuthorize]` attributes on a single field/mutation they are treated as `AND` meaning all claims are required. If you provide a single `[GraphQLAuthorize]` attribute with multiple claims in it they are treated as `OR` i.e. having any of the claims listed will grant access.

# Mutations

Mark you mutation methods with the `[GraphQLAuthorize("claim-name")]` attribute.

```
public class MovieMutations
{
  [GraphQLMutation]
  [GraphQLAuthorize("movie-editor")]
  public Movie AddActor(MyDbContext db, ActorArgs args)
  {
    // ...
  }
}
```

If a `ClaimsIdentity` is provided with the query call it will be required to be Authorized and have a claim of type `Role` with a value of `movie-editor` to call this mutation.

# Queries

If you are using the `SchemaBuilder.FromObject<TContext>()` you can use the `[GraphQLAuthorize("claim-name")]` attribute again throughout the objects.

```
public class MyDbContext : DbContext {
  protected override void OnModelCreating(ModelBuilder builder) {
    // Set up your relations
  }

  // require either claim
  [GraphQLAuthorize("property-role", "admin-property-role")]
  public DbSet<Property> Properties { get; set; }
  public DbSet<PropertyType> PropertyTypes { get; set; }
  public DbSet<Location> Locations { get; set; }
}

public class Property {
  public uint Id { get; set; }
  public string Name { get; set; }
  public PropertyType Type { get; set; }
  // require both claims
  [GraphQLAuthorize("property-admin")]
  [GraphQLAuthorize("super-admin")]
  public Location Location { get; set; }
}

// ....
```

You can secure whole types with the attribute too.

```
[GraphQLAuthorize("property-user")]
public class Property {
  public uint Id { get; set; }
  public string Name { get; set; }
  public PropertyType Type { get; set; }
  public Location Location { get; set; }
}
```

If a `ClaimsIdentity` is provided with the query call it will be required to be Authorized and have a claim of type `Role` with a value of `property-role` to query the root-level `properties` field and a claim of `property-admin` to query the `Property` field `location`.

`AuthorizeClaims` can be provided in the API for add/replacing fields on the schema objact.

```
schemaProvider.AddField("myField", (db) => db.MyEntities, "Description").RequiresAllClaims("admin");
schemaProvider.AddField("myField", (db) => db.MyEntities, "Description").RequiresAnyClaim("admin", "super-admin");

schemaProvider.AddType<Property>("properties", (db) => db.Properties, "Description").RequiresAllClaims("property-user");
schemaProvider.AddType<Property>("properties", (db) => db.Properties, "Description").RequiresAnyClaims("property-user", "property-admin");
```

Note when using `AddField()` and `AddType()` these functions will automatically search for `[GraphQLAuthorize()]` attributes on the fields and types.