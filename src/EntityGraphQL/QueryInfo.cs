using System.Collections.Generic;

namespace EntityGraphQL;

/// <summary>
/// GraphQL operation types
/// </summary>
public enum GraphQLOperationType
{
    Query,
    Mutation,
    Subscription,
}

/// <summary>
/// Information about the executed query that can be included in the result extensions
/// </summary>
public class QueryInfo
{
    /// <summary>
    /// The type of operation (query, mutation, subscription)
    /// </summary>
    public GraphQLOperationType OperationType { get; set; } = GraphQLOperationType.Query;

    /// <summary>
    /// The name of the operation (if provided)
    /// </summary>
    public string? OperationName { get; set; }

    /// <summary>
    /// Types queried and their selected fields
    /// </summary>
    public Dictionary<string, HashSet<string>> TypesQueried { get; set; } = new();

    /// <summary>
    /// Total number of fields queried across all types
    /// </summary>
    public int TotalFieldsQueried { get; set; }

    /// <summary>
    /// Total number of types queried
    /// </summary>
    public int TotalTypesQueried { get; set; }
}
