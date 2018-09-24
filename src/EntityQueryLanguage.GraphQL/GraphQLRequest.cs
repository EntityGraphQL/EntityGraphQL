using System.Collections.Generic;

namespace EntityQueryLanguage.GraphQL
{
    public class GraphQLRequest
    {
        public string OperationName { get; set; }
        public string Query { get; set; }
        public Dictionary<string, string> Variables { get; set; }
    }
}