---
title: "Paging"
metaTitle: "Add paging to your schema - EntityGraphQL"
metaDescription: "Add paging to your GraphQL schema"
---

Paging can be handled in a few different ways depending on your requirements and how you want your API to work.

# Simple Skip/Take

A simple example is to use `skip` and `take` arguments in your collection fields. For example in the schema we have been working with we could modify the `movies` fields (and other collections) like so.

```
schemaProvider.ReplaceField(
  "movies",
  new {
    take = 10, // defaults
    skip = 0
  },
  (db, args) => db.Movies
    .Skip(args.skip)
    .Take(args.take),
  "Get a page of movies"
);
```

The API user will just have to keep asking for more items until it doesn't get any results.

# Connection Model

Often you will want some meta data about the collection - total items, is there a next page, etc. GraphQL doesn't specify any particular way to do this, although the community (and [Relay](https://relay.dev/graphql/connections.htm)) have come up with the [Connection Model](https://graphql.org/learn/pagination/). EntityGraphQL contains an easy helper method to implement this in your schema.

```
schemaProvider.ReplaceField("movies",
  db => db.Movies.OrderBy(e => e.Id) // best to give the field a standard order so pages are the same
  "Get a page of movies"
)
.MakeConnection();
```

This will make the field return a type of `MovieConnection`, which is built from the .NET type below where `TEntity` would be `Movie`.

```
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
```

This follows this Relay Connection Model pattern and lets you page through data easily. And as in the code it is best to give your collection an order otherwise depending on the underlying data source the order could change over pages. You can order base on other arguments you add `MakeConnection()` will merge the arguments together.

```
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
.MakeConnection();
```
