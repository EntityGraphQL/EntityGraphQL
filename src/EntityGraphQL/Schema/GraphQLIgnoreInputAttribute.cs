using System;
using System.Collections.Generic;
using System.Text;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Tell the Introspection INPUT Schema to ignore this field or property
    /// </summary>
    public class GraphQLIgnoreInputAttribute : Attribute
    {
        public GraphQLIgnoreInputAttribute() { }
    }
}
