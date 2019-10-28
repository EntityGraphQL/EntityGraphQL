using System;
using System.Reflection;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Tell the Schema Builder to ignore this field or property
    /// </summary>
    public class GraphQLIgnoreAttribute : Attribute
    {
        public GraphQLIgnoreAttribute(GraphQLIgnoreType from = GraphQLIgnoreType.All)
        {
            IgnoreFrom = from;
        }

        public GraphQLIgnoreType IgnoreFrom { get; }

        /// <summary>
        /// Property is marked as being ignored for inclusion in the Query schema
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool ShouldIgnoreMemberFromQuery(MemberInfo prop)
        {
            var attribute = prop.GetCustomAttribute(typeof(GraphQLIgnoreAttribute)) as GraphQLIgnoreAttribute;
            if (attribute != null)
            {
                if (attribute.IgnoreFrom == GraphQLIgnoreType.All || attribute.IgnoreFrom == GraphQLIgnoreType.Query)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Property is marked as being ignored for inclusion in the Mutation Input types
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool ShouldIgnoreMemberFromInput(MemberInfo prop)
        {
            var attribute = prop.GetCustomAttribute(typeof(GraphQLIgnoreAttribute)) as GraphQLIgnoreAttribute;
            if (attribute != null)
            {
                if (attribute.IgnoreFrom == GraphQLIgnoreType.All || attribute.IgnoreFrom == GraphQLIgnoreType.Input)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public enum GraphQLIgnoreType
    {
        /// <summary>
        /// Ignored in generating the schema for Query
        /// </summary>
        Query,
        /// <summary>
        /// Ignored in generating/deserialising the input types
        Input,
        /// <summary>
        /// Ignored completely by EntityGraphQL
        /// </summary>
        All,
    }
}