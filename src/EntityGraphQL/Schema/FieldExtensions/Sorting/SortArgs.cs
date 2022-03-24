
namespace EntityGraphQL.Schema.FieldExtensions
{
    public class SortInput<T> where T : notnull
    {
        public T Sort { get; set; } = default!;
    }

    public enum SortDirectionEnum
    {
        ASC,
        DESC
    }
}