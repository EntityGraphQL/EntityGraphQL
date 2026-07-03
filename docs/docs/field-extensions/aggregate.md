---
sidebar_position: 4
---

# Aggregate

The `UseAggregate()` field extension exposes aggregate data — `count` plus `min`/`max`/`sum`/`average` over the fields of a collection — computed over the **whole collection** the field resolves to (not just a page). It builds the aggregates as part of the same query expression so they translate to a single SQL query under Entity Framework.

```cs
schema.ReplaceField("people",
    ctx => ctx.People,
    "Return a list of people")
    .UseAggregate();
```

If you are using `SchemaBuilder.FromObject` you can use the `UseAggregateAttribute` on your collection properties.

```cs
public class DemoContext : DbContext
{
    [UseAggregate]
    public DbSet<Person> People { get; set; }
}
```

This extension can only be used on a field whose `Resolve` expression is a collection (`IEnumerable`/`IQueryable`).

## The aggregate shape

The aggregate data is **function-first** (the same convention as Hasura). A generated `{Element}Aggregate` type exposes:

- `count` — the number of items
- `min` / `max` — over every comparable field (numbers, dates, strings)
- `sum` / `average` — over numeric fields only (`short`, `int`, `long`, `float`, `double`, `decimal` and their nullable forms; `short` is widened to `int` for these two)

For a `Person` with `id: Int`, `height: Float` and `birthday: Date`:

```graphql
type PersonAggregate {
  count: Int!
  min: PersonMinAggregate
  max: PersonMaxAggregate
  sum: PersonSumAggregate
  average: PersonAverageAggregate
}

type PersonMinAggregate { id: Int, height: Float, birthday: Date }
type PersonMaxAggregate { id: Int, height: Float, birthday: Date }
type PersonSumAggregate { id: Int, height: Float }
type PersonAverageAggregate { id: Float, height: Float }
```

`average` always returns a floating point type. A function that has no applicable fields (e.g. `sum` when the element has no numeric fields) is not generated.

You query it like:

```graphql
{
  peopleAggregate {
    count
    min { height }
    max { height }
    average { height }
  }
}
```

## Choosing the fields

By default every aggregatable scalar field is exposed. Pass a field selection to restrict it to specific fields, either an anonymous object of members or a single member:

```cs
schema.ReplaceField("people", ctx => ctx.People, "People")
    .UseAggregate((Person p) => new { p.Height, p.Age });

schema.ReplaceField("people", ctx => ctx.People, "People")
    .UseAggregate((Person p) => p.Height);
```

The generated `{Element}Aggregate` type is built and reused per element type, so apply a consistent selection when aggregating the same type from multiple fields.

## Placement

Where the aggregate data is exposed is controlled by `AggregatePlacement`. The default is `Auto`.

### `Auto` (default)

Picks the placement that works for the field's shape:

- the field is paged → `PagingWrapper`
- otherwise → `OwnWrapper`

`OwnWrapper` is the default for unpaged fields because it handles every field uniformly (filtering, custom arguments, service-backed resolvers). `SiblingField` is never selected automatically — choose it explicitly when you want the additive behavior (see below).

### `SiblingField`

Adds a new field named `{field}Aggregate` next to the original collection field. The original field is unchanged.

```cs
schema.ReplaceField("people", ctx => ctx.People, "People")
    .UseAggregate(AggregatePlacement.SiblingField);
```

```graphql
{
  people { id name }
  peopleAggregate { count max { height } }
}
```

Because it's additive (it doesn't change the original field's shape), `SiblingField` is the safe way to add aggregates to an **existing** field without a breaking change — clients keep querying `people { ... }` and gain `peopleAggregate { ... }`.

The sibling carries the source field's arguments, so it can be filtered/parameterised the same way (e.g. `peopleAggregate(filter: "...", minId: 5)`). Note these are supplied to the two fields independently, so it's up to the caller to keep them in sync; `PagingWrapper`/`OwnWrapper` avoid that by exposing one field. `SiblingField` cannot be used on a service-backed collection resolver (a separate field would invoke the service twice) — use `OwnWrapper`/`PagingWrapper` there.

```cs
schema.ReplaceField("people", ctx => ctx.People, "People")
    .UseFilter()
    .UseAggregate(AggregatePlacement.SiblingField);
```

```graphql
{
  people(filter: "id > 1") { id }
  peopleAggregate(filter: "id > 1") { count }
}
```

### `PagingWrapper`

Attaches an `aggregate` field onto the paging wrapper (`OffsetPage`/`Connection`) the field already returns. Chain `UseAggregate()` **after** the paging extension. The aggregate is computed over the full filtered collection, not just the current page — and because it is one field the filter/sort arguments cannot drift between the items and the aggregate.

```cs
schema.ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "People")
    .UseFilter()
    .UseOffsetPaging()
    .UseAggregate(AggregatePlacement.PagingWrapper);
```

```graphql
{
  people(filter: "id > 1", take: 10) {
    items { id name }
    totalItems
    aggregate { count max { height } }
  }
}
```

### `OwnWrapper`

Replaces an unpaged field's return type with a generated `{Element}WithAggregate` wrapper exposing `items` and `aggregate` under one field with shared arguments. Any `UseFilter()` on the field applies to both.

```cs
schema.ReplaceField("people", ctx => ctx.People, "People")
    .UseFilter()
    .UseAggregate(AggregatePlacement.OwnWrapper);
```

```graphql
{
  people(filter: "id > 1") {
    items { id name }
    aggregate { count min { height } }
  }
}
```

## Custom arguments and services

All placements carry the field's own resolver arguments. For example a field defined with a `minId` argument:

```cs
schema.Query()
    .AddField("people", new { minId = 0 }, (ctx, args) => ctx.People.Where(p => p.Id >= args.minId), "People")
    .UseAggregate(); // Auto -> OwnWrapper
```

```graphql
{
  people(minId: 5) {
    items { id }
    aggregate { count }   # aggregate also only counts id >= 5
  }
}
```

With `OwnWrapper`/`PagingWrapper` the argument is on the single field, so `items` and `aggregate` always share the same value. With `SiblingField` the two fields take the argument independently (`people(minId: 5)` and `peopleAggregate(minId: 5)`), so the caller keeps them in sync.

### Service-backed fields

A collection field whose resolver uses a service (`.Resolve<TService>((ctx, svc) => svc.Get())`) is materialized once; the aggregate is then computed over that **single** result (the service is not re-invoked per aggregate value). This is supported with `OwnWrapper` and `PagingWrapper`, both of which keep the original field. `Auto` selects `OwnWrapper` for an unpaged service field:

```cs
schema.Query()
    .AddField("people", "People from a service")
    .Resolve<PeopleService>((ctx, svc) => svc.Get())
    .UseAggregate(); // Auto -> OwnWrapper
```

With paging, the aggregate runs over the same set as the page — including an interleaved service in the collection resolver:

```cs
schema.Query()
    .ReplaceField("people", "People")
    .Resolve<PeopleService>((ctx, svc) => ctx.People.Where(p => svc.Keep(p.Id)).OrderBy(p => p.Id))
    .UseOffsetPaging()
    .UseAggregate(AggregatePlacement.PagingWrapper);
```

`SiblingField` is a separate field and would invoke the service a second time, so it cannot be used with a service-backed collection resolver and throws at schema-build time. (A paged field that merely has service-backed fields in its element selection works with every mode — only the collection *resolver* itself needs the wrapper modes.)

### Aggregating a service-backed field

A field on the element type whose value is resolved by a service can also be aggregated:

```cs
schema.Type<Person>().AddField("score", "computed score").Resolve<ScoreService>((p, svc) => svc.Score(p.Id));
schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseAggregate();
```

```graphql
{
  people { aggregate { sum { score } max { score } average { score } } }
}
```

The per-element values the service needs are projected and materialized in the first (DB) pass, then the reduction runs in memory with the service in the second pass — so this works under EF even though the service itself isn't translatable to SQL.

If the field uses a bulk resolver (`ResolveBulk`), aggregation keeps the bulk optimization: the keys are materialized in the first pass and all values are fetched in a single bulk call in the second pass, then reduced — rather than one service call per element. (Applies to sync, no-argument bulk resolvers; async or argument-taking bulk resolvers fall back to the per-element resolver.)

## Ordering with other extensions

When combining with filter, sort and paging, the order matters: `Filter -> Sort -> Paging -> Aggregate`. `UseAggregate()` must come last so it can attach to the paging wrapper (for `Auto`/`PagingWrapper`) and so its aggregate respects the filter.
