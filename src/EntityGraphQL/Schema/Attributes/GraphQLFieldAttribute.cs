using System;
using System.Collections.Generic;
using System.Text;

namespace EntityGraphQL.Schema
{
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
}
