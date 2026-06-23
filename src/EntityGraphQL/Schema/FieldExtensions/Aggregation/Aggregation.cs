namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Marker type that backs a generated GraphQL aggregate object type (e.g. PersonAggregate).
/// It is never materialized at runtime - the aggregate field resolves to the source collection and
/// the object-projection machinery builds a new {} of the requested aggregate values from it.
/// Generic on the element type so each element type gets a unique, reusable aggregate type.
/// </summary>
/// <typeparam name="T">The element type of the collection being aggregated.</typeparam>
public class Aggregation<T> { }

/// <summary>
/// Marker type that backs a generated "{Element}WithAggregate" wrapper exposing { items, aggregate }
/// for the OwnWrapper placement. Never materialized.
/// </summary>
/// <typeparam name="T">The element type of the collection being aggregated.</typeparam>
public class AggregateWithItems<T> { }

/// <summary>
/// How a <see cref="AggregateExtension"/> exposes the aggregate data for a collection field.
/// </summary>
public enum AggregatePlacement
{
    /// <summary>
    /// Attach to an existing paging wrapper (OffsetPage/Connection) if one is present, otherwise add a sibling field.
    /// </summary>
    Auto,

    /// <summary>
    /// Add a new sibling field named "{field}Aggregate" next to the original collection field. The original field is unchanged.
    /// </summary>
    SiblingField,

    /// <summary>
    /// Attach an "aggregate" field onto the paging wrapper (OffsetPage/Connection) the field already returns.
    /// The aggregate is computed over the full filtered collection (not just the current page).
    /// Requires UseAggregate() to be chained after UseOffsetPaging()/UseConnectionPaging().
    /// </summary>
    PagingWrapper,

    /// <summary>
    /// Replace the field's return type with a new "{Element}WithAggregate" wrapper exposing { items, aggregate }.
    /// Use for an unpaged collection field when you want items and aggregate under one field with shared arguments.
    /// </summary>
    OwnWrapper,
}
