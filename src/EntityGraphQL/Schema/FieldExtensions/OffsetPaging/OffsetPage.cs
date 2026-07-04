using System.Collections.Generic;
using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions;

public class OffsetPage<T>
{
    private readonly int? skip;
    private readonly int? take;

    // set when hasNextPage is requested without totalItems - answered with a cheap EXISTS query instead of a full COUNT
    private readonly bool? hasNextPageOverride;

    public OffsetPage(int? skip, int? take)
        : this(skip, take, null) { }

    public OffsetPage(int? skip, int? take, bool? hasNextPageOverride)
    {
        this.skip = skip;
        this.take = take;
        this.hasNextPageOverride = hasNextPageOverride;
    }

    [Description("Items in the page")]
    public IEnumerable<T> Items { get; set; } = new List<T>();

    [Description("True if there is more data before this page")]
    public bool HasPreviousPage => (skip ?? 0) > 0;

    [Description("True if there is more data after this page")]
    public bool HasNextPage => hasNextPageOverride ?? (take != null && ((skip ?? 0) + (take ?? 0)) < TotalItems);

    [Description("Count of the total items in the collection")]
    public int TotalItems { get; set; }
}
