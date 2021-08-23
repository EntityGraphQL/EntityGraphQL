using System.Collections.Generic;
using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class OffsetPage<T>
    {
        private readonly int? skip;
        private readonly int? take;

        public OffsetPage(int totalItems, int? skip, int? take)
        {
            TotalItems = totalItems;
            this.skip = skip;
            this.take = take;
        }
        [Description("Items in the page")]
        public IEnumerable<T> Items { get; set; }
        [Description("True if there is more data before this page")]
        public bool HasPreviousPage => skip > 0;
        [Description("True if there is more data after this page")]
        public bool HasNextPage => (skip + take) < TotalItems;
        [Description("Count of the total items in the collection")]
        public int TotalItems { get; set; }
    }
}