
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Authorization
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class GraphQLAuthorizeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the GraphQLAuthorizeAttribute class 
        /// </summary>
        public GraphQLAuthorizeAttribute()
        { }
        /// <summary>
        /// Initializes a new instance of the GraphQLAuthorizeAttribute class with the specified roles.
        /// </summary>
        /// <param name="roles"></param>
        public GraphQLAuthorizeAttribute(params string[] roles)
        {
            Roles = roles.ToList();
        }

        [Obsolete("Use Roles instead")]
        public List<string> Claims { get => Roles; set => Roles = value; }
        /// <summary>
        /// Gets or sets the roles name that determines access to the resource.
        /// </summary>
        public List<string> Roles { get; set; }
    }
}