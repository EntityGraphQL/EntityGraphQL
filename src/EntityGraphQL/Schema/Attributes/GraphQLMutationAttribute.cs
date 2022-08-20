using System;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Marks the method in the class as a Mutation for EntityGraphQL to include in the Mutation Type.
    /// You need to add the mutation to the schema using <code>schema.AddMutationFrom<MyClass>();</code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class GraphQLMutationAttribute : Attribute
    {
        public GraphQLMutationAttribute(string description = "")
        {
            this.Description = description;
        }

        public string Description { get; set; }
    }

    /// <summary>
    /// Have your mutation argument class implement this interface.
    /// Allows EntityGraphQL to know which argument of the mutation needs to be
    /// populated with the mutation arguments from the query
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter)]
    public class MutationArgumentsAttribute : Attribute { }
}