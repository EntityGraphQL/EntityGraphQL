
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.AspNet
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class GraphQLAuthorizePolicyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the GraphQLAuthorizeAttribute class 
        /// </summary>
        public GraphQLAuthorizePolicyAttribute()
        {
            Policies = new List<string>();
        }
        /// <summary>
        /// Initializes a new instance of the GraphQLAuthorizePolicyAttribute class with the specified policies.
        /// </summary>
        /// <param name="policies"></param>
        public GraphQLAuthorizePolicyAttribute(params string[] policies)
        {
            Policies = policies.ToList();
        }

        /// <summary>
        /// Gets or sets the policies name that determines access to the resource.
        /// </summary>
        public List<string> Policies { get; set; }
    }
}