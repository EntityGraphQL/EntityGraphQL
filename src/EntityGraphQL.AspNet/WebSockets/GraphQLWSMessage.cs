using System.Collections.Generic;

namespace EntityGraphQL.AspNet.WebSockets;

public class BaseGraphQLWSResponse
{
    public string Type { get; set; } = string.Empty;
}

public class BaseWithIdGraphQLWSResponse : BaseGraphQLWSResponse
{
    public string? Id { get; set; }
}

public class GraphQLWSRequest : BaseWithIdGraphQLWSResponse
{
    public QueryRequest? Payload { get; set; }
}

public class GraphQLWSResponse : BaseWithIdGraphQLWSResponse
{
    public QueryResult? Payload { get; set; }
}

public class GraphQLWSError : BaseWithIdGraphQLWSResponse
{
    public List<GraphQLError>? Payload { get; set; }
}
