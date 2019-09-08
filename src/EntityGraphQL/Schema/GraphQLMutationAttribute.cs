using System;

namespace EntityGraphQL.Schema
{
    public class GraphQLMutationAttribute : Attribute
    {
		public GraphQLMutationAttribute(string description = null)
        {
            this.Description = description;
        }

		public string Description { get; set; }
	}
}