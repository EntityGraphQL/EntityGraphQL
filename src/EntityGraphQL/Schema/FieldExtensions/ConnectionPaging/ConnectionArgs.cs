namespace EntityGraphQL.Schema.FieldExtensions
{
    public class ConnectionArgs
    {
        // forward pagination
        public int? First { get; set; } = null;
        public string After { get; set; } = null;
        [GraphQLIgnore]
        public int? AfterNum { get; set; } = null;
        // backward pagination
        public int? Last { get; set; } = null;
        public string Before { get; set; } = null;
        [GraphQLIgnore]
        public int? BeforeNum { get; set; } = null;
        // On creation of Connection<> we store the total count here to avoid having to execute it multiple times
        [GraphQLIgnore]
        public int TotalCount { get; set; }
    }
}