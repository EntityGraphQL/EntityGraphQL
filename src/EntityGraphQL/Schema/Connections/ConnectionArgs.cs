namespace EntityGraphQL.Schema.Connections
{
    public class ConnectionArgs
    {
        // forward pagination
        public int? first { get; set; } = null;
        public string after { get; set; } = null;
        [GraphQLIgnore]
        public int? afterNum { get; set; } = null;
        // backward pagination
        public int? last { get; set; } = null;
        public string before { get; set; } = null;
        [GraphQLIgnore]
        public int? beforeNum { get; set; } = null;
        // On creation of Connection<> we store the total count here to avoid having to execute it multiple times
        [GraphQLIgnore]
        public int totalCount { get; set; }
    }
}