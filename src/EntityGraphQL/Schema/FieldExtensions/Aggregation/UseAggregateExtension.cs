namespace EntityGraphQL.Schema.FieldExtensions;

public static class UseAggregateExtension
{
    /// <summary>
    /// Expose aggregate data (count + min/max/sum/average over numeric fields) for a collection field.
    /// The aggregate is computed over the whole collection the field resolves to.
    /// Only call on a field that returns a collection (IEnumerable/IQueryable).
    /// </summary>
    /// <param name="field"></param>
    /// <param name="placement">How the aggregate data is exposed. Defaults to Auto: attaches to an existing
    /// paging wrapper if the field is paged, otherwise adds a sibling "{field}Aggregate" field.</param>
    /// <returns></returns>
    public static IField UseAggregate(this IField field, AggregatePlacement placement = AggregatePlacement.Auto)
    {
        field.AddExtension(new AggregateExtension(placement));
        return field;
    }

    /// <summary>
    /// Expose aggregate data, restricted to the selected element fields.
    /// </summary>
    /// <param name="field"></param>
    /// <param name="fieldSelection">The element fields to expose aggregates for, e.g. <c>x => new { x.Height, x.Age }</c> or <c>x => x.Height</c>. T must be the element type of the collection.</param>
    /// <param name="placement">How the aggregate data is exposed. Defaults to Auto.</param>
    /// <returns></returns>
    public static IField UseAggregate<TElementType, TFields>(this IField field, System.Linq.Expressions.Expression<System.Func<TElementType, TFields>> fieldSelection, AggregatePlacement placement = AggregatePlacement.Auto)
    {
        field.AddExtension(new AggregateExtension(placement, fieldSelection));
        return field;
    }
}

public class UseAggregateAttribute : ExtensionAttribute
{
    public AggregatePlacement Placement { get; set; } = AggregatePlacement.Auto;

    public UseAggregateAttribute() { }

    public UseAggregateAttribute(AggregatePlacement placement)
    {
        Placement = placement;
    }

    public override void ApplyExtension(IField field)
    {
        field.UseAggregate(Placement);
    }
}
