namespace EntityGraphQL.Schema.FieldExtensions
{
    public class ConnectionArgs
    {
        // forward pagination
        public int? First { get; set; }
        public string? After { get; set; }
        [GraphQLIgnore]
        public int? AfterNum { get; set; } = null;
        // backward pagination
        public int? Last { get; set; }
        public string? Before { get; set; }
        [GraphQLIgnore]
        public int? BeforeNum { get; set; } = null;
        // On creation of Connection<> we store the total count here to avoid having to execute it multiple times
        [GraphQLIgnore]
        public int TotalCount { get; set; }
    }
}