---
title: "Field Extensions"
metaTitle: "Field Extensions - EntityGraphQL"
metaDescription: "Extending your schema fields in EntityGraphQL"
---

EntityGraphQL provides field extension methods for modifying you field expressions with common use cases, such as paging collections.

The following highlights the provided field extensions in EntityGraphQL available as well as an introduction to creating your own.

- [`UseFilter`](/field-extensions/02-filtering) to add expression based filtering to collections
- [`UseSort`](/field-extensions/03-sorting) to add a `sort` argument to your collections
- [Paging](/field-extensions/01-paging)
    - [`UseConnectionPaging`](/field-extensions/01-paging#connectionpagingmodel) for paging collections
    - [`UseOffsetPaging`](/field-extensions/01-paging#offsetpaging) for paging collections
- [Creating](/field-extensions/04-custom-extensions) your own Field Extensions

When combining multiple extensions together on a single field *order matters*. The correct order is
> Filter -> Sort -> Paging
