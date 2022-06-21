using System;
using System.Collections.Generic;
using System.Text;

namespace EntityGraphQL.Schema
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class GraphQLOneOfAttribute : Attribute
    {
    }
}
