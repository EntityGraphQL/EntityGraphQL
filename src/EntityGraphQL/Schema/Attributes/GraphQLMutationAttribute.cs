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
}