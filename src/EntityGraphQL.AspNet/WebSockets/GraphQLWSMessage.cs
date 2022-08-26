using System;
using System.Collections.Generic;

namespace EntityGraphQL.AspNet.WebSockets
{
    public class TypeOnlyGraphQLWSResponse
    {
        public string? Type { get; set; }
    }

    public class WithIdGraphQLWSResponse : TypeOnlyGraphQLWSResponse
    {
        public Guid? Id { get; set; }
    }
    public class GraphQLWSRequest : WithIdGraphQLWSResponse
    {
        public QueryRequest? Payload { get; set; }
    }

    public class GraphQLWSResponse : WithIdGraphQLWSResponse
    {
        public QueryResult? Payload { get; set; }
    }

    public class GraphQLWSError : WithIdGraphQLWSResponse
    {
        public List<GraphQLError>? Payload { get; set; }
    }
}