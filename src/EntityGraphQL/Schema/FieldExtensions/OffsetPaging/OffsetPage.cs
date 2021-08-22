using System.Collections.Generic;

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

        public IEnumerable<T> Items { get; set; }
        public bool HasPreviousPage => skip > 0;
        public bool HasNextPage => (skip + take) < TotalItems;
        public int TotalItems { get; set; }
    }
}