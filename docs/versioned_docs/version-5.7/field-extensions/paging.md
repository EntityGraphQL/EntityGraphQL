---
sidebar_position: 3
---

# Paging

Paging can be handled in any way you can build your field expressions. It all depends on your requirements and how you want your API to work.

The API user will just have to keep asking for more items until it doesn't get any results.

Note: When using one of the below field extensions with any of the other field extensions, make sure the paging one is added last. Filter -> Sort -> Paging.

## Connection Paging Model

Quickly you'll see the above lacks useful metadata - are there more items? What is the total items? etc. GraphQL doesn't have requirements on a particular way to do this, although the community (e.g. [Relay](https://relay.dev/graphql/connections.htm)) have landed on the [Connection Model](https://graphql.org/learn/pagination/). EntityGraphQL contains an easy field extension method to implement this in your schema - `UseConnectionPaging()`. This can only be applied to fields that return a collection type.

```cs
schemaProvider.ReplaceField("movies",
  db => db.Movies.OrderBy(e => e.Id) // best to give the field an order so pages are the same
  "Get a page of movies"
)
.UseConnectionPaging();
```

If you are using the `SchemaBuilder.FromObject` you can use the `UseConnectionPagingAttribute` on your collection properties. It takes the same arguments as `UseConnectionPaging()` outlined below.

```cs
public class DemoContext : DbContext
{
    [UseConnectionPaging]
    public DbSet<Movie> Movies { get; set; }
    [UseConnectionPaging]
    public DbSet<Person> People { get; set; }
    [UseConnectionPaging]
    public DbSet<Actor> Actors { get; set; }
}
```

This will make the field return a schema type of `MovieConnection`, which is built from the .NET type below where `TEntity` would be `Movie`.

```cs
public class Connection<TEntity>
{
  [GraphQLNotNull]
  [Description("Edge information about each node in the collection")]
  public IEnumerable<ConnectionEdge<TEntity>> Edges { get; set; }

  [GraphQLNotNull]
  [Description("Total count of items in the collection")]
  public int TotalCount { get; set; }

  [GraphQLNotNull]
  [Description("Information about this page of data")]
  public ConnectionPageInfo PageInfo { get; set; }
}

public class ConnectionEdge<TEntity>
{
  [GraphQLNotNull]
  [Description("The item of the collection")]
  public TEntity Node { get; set; }

  [GraphQLNotNull]
  [Description("The cursor for this items position within the collection")]
  public string Cursor { get; set; }
}
```

This follows the Relay Connection Model pattern and lets you page through data easily with code using the cursor metadata. As in the example above you should give your collection an order otherwise depending on the underlying data source the order could change over pages/queries. You can order base on other arguments you have on the field, `UseConnectionPaging()` will merge the arguments together.

```cs
schemaProvider.ReplaceField("movies",
  new {
    year = (int?)null,
    orderByRelease = (bool?)null
  },
  (db, args) => db.Movies
    .WhereWhen(m => m.Released.Year == args.year, args.year.HasValue)
    .OrderBy(e => args.orderByRelease ? e.Released : e.Id)
  "Get a page of movies"
)
.UseConnectionPaging();
```

Below shows the available fields on the new connection type (as always you can explore you schema in something like GraphiQL).

```graphql
movies(first: 4, after: "MQ==") {
    edges {
      cursor
      node { # this is your expected Movie context
        name
      }
    }
    pageInfo {
      hasPreviousPage
      hasNextPage
      startCursor
      endCursor
    }
    totalCount
  }
```

### Default & Max Page Size

You can set an optional default page size or max page size for the connection paging model.

```cs
myField
.UseConnectionPaging(
  defaultPageSize: 10,
  maxPageSize: 50
)
```

If `first` and `last` arguments are `null` the `defaultPageSize` value will be set to the `first` argument.

If either `first` or `last` arguments are greater then the `maxPageSize` value an error is raised and the query will fail.

### Connection Model Implementation

The `Cursor` in the connection paging model is built on the row index of the item. As suggested in the [connection model](https://graphql.org/learn/pagination/#complete-connection-model) the row index is encoded.

> As a reminder that the cursors are opaque and that their format should not be relied upon, we suggest base64 encoding them.

## Offset Paging

EntityGraphQL also provides a offset based (`Skip`/`Take`) extension method to apply a offset based paging model to your fields.

```cs
schemaProvider.ReplaceField("movies",
  db => db.Movies.OrderBy(e => e.Id) // best to give the field an order so pages are the same
  "Get a page of movies"
)
.UseOffsetPaging();
```

If you are using the `SchemaBuilder.FromObject` you can use the `UseOffsetPagingAttribute` on your collection properties. It takes the same arguments as `UseOffsetPaging()` outlined below.

```cs
public class DemoContext : DbContext
{
    [UseOffsetPaging]
    public DbSet<Movie> Movies { get; set; }
    [UseOffsetPaging]
    public DbSet<Person> People { get; set; }
    [UseOffsetPaging]
    public DbSet<Actor> Actors { get; set; }
}
```

This will make the field return a schema type of `MovieOffsetPage`, which is built from the .NET type below where `TEntity` would be `Movie`.

```cs
public class OffsetPage<TEntity>
{
    [Description("Items in the page")]
    public IEnumerable<TEntity> Items { get; set; }

    [Description("True if there is more data before this page")]
    public bool HasPreviousPage { get; set; }

    [Description("True if there is more data after this page")]
    public bool HasNextPage { get; set; }

    [Description("Count of the total items in the collection")]
    public int TotalItems { get; set; }
}
```

### Default & Max Page Size

You can set an optional default page size or max page size for the offset paging model.

```cs
myField
.UseOffsetPaging(
  defaultPageSize: 10,
  maxPageSize: 50
)
```

If the `take` argument is `null` the `defaultPageSize` value will be set to it.

If the `take` argument is greater then the `maxPageSize` value an error is raised and the query will fail.

### Custom Paging

A simple example is to use `skip` and `take` arguments in your collection fields. For example in the schema we have been working with we could modify the `movies` field (and other collections) like so.

```cs
schemaProvider.ReplaceField(
  "movies",
  new { // add our field arguments
    take = 10, // defaults
    skip = 0
  },
  (db, args) => db.Movies
    .Skip(args.skip) // do paging
    .Take(args.take),
  "Get a page of movies"
);
```
