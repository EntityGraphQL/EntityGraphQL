using System.Collections.Generic;
using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions;

public class OffsetPage<T>(int totalItems, int? skip, int? take)
{
    private readonly int? skip = skip;
    private readonly int? take = take;

    [Description("Items in the page")]
    public IEnumerable<T> Items { get; set; } = new List<T>();

    [Description("True if there is more data before this page")]
    public bool HasPreviousPage => (skip ?? 0) > 0;

    [Description("True if there is more data after this page")]
    public bool HasNextPage => take != null && ((skip ?? 0) + (take ?? 0)) < TotalItems;

    [Description("Count of the total items in the collection")]
    public int TotalItems { get; set; } = totalItems;
}
