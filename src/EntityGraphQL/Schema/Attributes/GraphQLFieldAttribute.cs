using System;

namespace EntityGraphQL.Schema;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false)]
public class GraphQLFieldAttribute : Attribute
{
    public GraphQLFieldAttribute(string? name = null, string? description = null)
    {
        Name = name;
        Description = description;
    }

    public string? Name { get; }
    public string? Description { get; }
}