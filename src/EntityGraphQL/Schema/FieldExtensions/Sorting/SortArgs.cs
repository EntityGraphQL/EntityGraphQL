using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class PersonSortArgs
    {
        public SortDirectionEnum? Id { get; set; }
        public SortDirectionEnum? Name { get; set; }

        [Description("Order by")]
        public SortDirectionEnum? Genre { get; set; }
        public SortDirectionEnum? Released { get; set; }
        public SortDirectionEnum? Actors { get; set; }
        public SortDirectionEnum? Writers { get; set; }
        public SortDirectionEnum? Director { get; set; }
        public SortDirectionEnum? DirectorId { get; set; }
        [Description("If set sorts the collection by the Rating field")]
        public SortDirectionEnum? Rating { get; set; }
    }

    public enum SortDirectionEnum
    {
        ASC,
        DESC
    }
}