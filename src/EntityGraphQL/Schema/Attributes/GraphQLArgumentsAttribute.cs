using System;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Have your mutation/subscription argument class implement this interface.
    /// Allows EntityGraphQL to know which argument of the mutation/subscription needs to be
    /// populated with the mutation/subscription arguments from the query
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter)]
    public class GraphQLArgumentsAttribute : Attribute { }
}