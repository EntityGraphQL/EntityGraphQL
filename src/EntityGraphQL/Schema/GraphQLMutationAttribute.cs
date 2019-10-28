using System;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Marks the method in the class as a Mutation for EntityGraphQL to include in the Mutation Type.
    /// You need to add the mutation to the schema using <code>schema.AddMutationFrom(new MyClass());</code>
    /// </summary>
    public class GraphQLMutationAttribute : Attribute
    {
        public GraphQLMutationAttribute(string description = null)
        {
            this.Description = description;
        }

        public string Description { get; set; }
    }
}