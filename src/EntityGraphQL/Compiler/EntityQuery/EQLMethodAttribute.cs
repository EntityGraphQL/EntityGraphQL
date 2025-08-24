using System;

namespace EntityGraphQL.Compiler.EntityQuery;

/// <summary>
/// Marks an extension method as safe for use in GraphQL filter expressions.
/// Only methods marked with this attribute will be exposed to the filter language when using EqlMethodProvider.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class EqlMethodAttribute : Attribute
{
    /// <summary>
    /// The name to use for this method in filter expressions. If not provided, uses the method name converted to camelCase.
    /// </summary>
    public string? MethodName { get; set; }

    /// <summary>
    /// Initializes a new instance of the GraphQLFilterMethodAttribute.
    /// </summary>
    public EqlMethodAttribute() { }

    /// <summary>
    /// Initializes a new instance of the GraphQLFilterMethodAttribute with a specific method name.
    /// </summary>
    /// <param name="methodName">The name to use for this method in filter expressions</param>
    public EqlMethodAttribute(string methodName)
    {
        MethodName = methodName;
    }
}
