using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class ConnectionEdge<TEntity>
    {
        [GraphQLNotNull]
        [Description("The item of the collection")]
        public TEntity? Node { get; set; }

        [GraphQLNotNull]
        [Description("The cursor for this items position within the collection")]
        public string? Cursor { get; set; }
    }
}