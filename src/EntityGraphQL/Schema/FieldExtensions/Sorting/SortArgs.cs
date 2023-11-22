
using System.Collections.Generic;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class SortInput<T> where T : notnull
    {
        public List<T>? Sort { get; set; }
    }

    public enum SortDirection
    {
        ASC,
        DESC
    }
}