# Field Extensions

EntityGraphQL provides field extension methods for modifying you field expressions with common use cases, such as paging collections.

The following highlights the provided field extensions in EntityGraphQL available as well as an introduction to creating your own.

- [`UseFilter`](./filtering) to add expression based filtering to collections
- [`UseSort`](./sorting) to add a `sort` argument to your collections
- [Paging](./paging)
  - [`UseConnectionPaging`](./paging#connection-paging-model) for paging collections
  - [`UseOffsetPaging`](./paging#offset-paging) for paging collections
- [`UseAggregate`](./aggregate) to expose aggregate data (`count` plus `min`/`max`/`sum`/`average`) over collections
- [Creating](./custom-extensions) your own Field Extensions

When combining multiple extensions together on a single field _order matters_. The correct order is for the provided extensions is

> Filter -> Sort -> Paging -> Aggregate

`UseAggregate` goes last because by default it adapts to the shape produced by the extensions before it: if the field is paged it attaches an `aggregate` field to the paging result, otherwise it wraps the field as `{ items, aggregate }`. An additive, non-breaking sibling `{field}Aggregate` field is also available as an opt-in. See [`UseAggregate`](./aggregate) for the placement options.
