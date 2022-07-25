using System;

namespace EntityGraphQL.Schema
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class GraphQLOneOfAttribute : Attribute
    {
    }
}
