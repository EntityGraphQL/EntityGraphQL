---
title: "Paging"
metaTitle: "Add paging to your schema - EntityGraphQL"
metaDescription: "Add paging to your GraphQL schema"
---

For paging you will want to add (or replace) your own fields.

```cs
schemaProvider.AddField(
  "movies",
  new {
    take = 10, // defaults
    skip = 0
  },
  (db, args) => db.MyEntities.Skip(args.skip).Take(args.take),
  "Get a page of movies"
);
```

Open to ideas for making this easier. We are looking into the common patterns used in GraphQL elsewhere.
