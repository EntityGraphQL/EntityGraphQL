using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class SortInput<T>
    {
        public T Sort { get; set; }
    }

    public enum SortDirectionEnum
    {
        ASC,
        DESC
    }
}