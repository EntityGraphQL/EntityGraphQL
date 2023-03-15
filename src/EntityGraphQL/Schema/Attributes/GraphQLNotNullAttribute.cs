using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Tell the Schema Builder that when building this field, it is not nullable in the schema
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class GraphQLNotNullAttribute : Attribute
    {
        public GraphQLNotNullAttribute()
        {
        }

        /// <summary>
        /// Check if property is marked as being not null
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool IsMemberMarkedNotNull(ICustomAttributeProvider prop)
        {
            return IsMemberMarkedNotNull(prop.GetCustomAttributes(false).Cast<Attribute>());
        }
        public static bool IsMemberMarkedNotNull(IEnumerable<Attribute> attributes)
        {
            if (attributes.Any(a => a is GraphQLNotNullAttribute) ||
                attributes.Any(a => a is RequiredAttribute))
            {
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Tells the schema builder that this the element type in the List/array of this field is nullable in the schema.
    /// By default a IEnumerable<T> will have the T as non-nullable in the GraphQL schema
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class GraphQLElementTypeNullableAttribute : Attribute
    {
        public GraphQLElementTypeNullableAttribute()
        {
        }

        /// <summary>
        /// Check if property is marked as being not null
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool IsMemberElementMarkedNullable(ICustomAttributeProvider prop)
        {
            if (prop.GetCustomAttributes(false).Any(a => a is GraphQLElementTypeNullableAttribute))
            {
                return true;
            }
            return false;
        }
    }
}