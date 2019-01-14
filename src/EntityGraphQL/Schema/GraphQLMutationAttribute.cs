using System;

namespace EntityGraphQL.Schema
{
    public class GraphQLMutationAttribute : Attribute
    {
        private string description;

        public GraphQLMutationAttribute(string description = null)
        {
            this.Description = description;
        }

        public string Description { get => description; set => description = value; }
    }
}