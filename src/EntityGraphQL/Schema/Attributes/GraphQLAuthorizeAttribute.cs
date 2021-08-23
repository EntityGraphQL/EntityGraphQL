
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Authorization
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class GraphQLAuthorizeAttribute : Attribute
    {
        //
        // Summary:
        //     Initializes a new instance of the EntityGraphQL.Authorization.GraphQLAuthorizeAttribute
        //     class.
        public GraphQLAuthorizeAttribute()
        { }
        //
        // Summary:
        //     Initializes a new instance of the EntityGraphQL.Authorization.GraphQLAuthorizeAttribute
        //     class with the specified policy.
        //
        // Parameters:
        //   policy:
        //     The name of the policy to require for authorization.
        public GraphQLAuthorizeAttribute(params string[] claims)
        {
            Claims = claims.ToList();
        }

        //
        // Summary:
        //     Gets or sets the policy name that determines access to the resource.
        public List<string> Claims { get; set; }
    }
}