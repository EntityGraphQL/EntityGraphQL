using System.ComponentModel;

namespace EntityGraphQL.Schema.Connections
{
    public class ConnectionPageInfo
    {
        [GraphQLNotNull]
        [Description("Last cursor in the page. Use this as the next from argument")]
        public string EndCursor { get; set; }
        [GraphQLNotNull]
        [Description("Start cursor in the page. Use this to go backwards with the before argument")]
        public string StartCursor { get; set; }
        [Description("If there is more data after this page")]
        public bool HasNextPage { get; set; }
        [Description("If there is data previous to this page")]
        public bool HasPreviousPage { get; set; }
    }
}