using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using EntityGraphQL.Authorization;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Checks if the executing user has the required roles to access the requested part of the GraphQL schema
    /// </summary>
    public class RoleBasedAuthorization : IGqlAuthorizationService
    {
        public RoleBasedAuthorization()
        {
        }

        /// <summary>
        /// Check if this user has the right security claims, roles or policies to access the request type/field
        /// </summary>
        /// <param name="requiredAuthorization">The required auth for the field or type you want to check against the user</param>
        /// <returns></returns>
        public virtual bool IsAuthorized(ClaimsPrincipal? user, RequiredAuthorization? requiredAuthorization)
        {
            // if the list is empty it means identity.IsAuthenticated needs to be true, if full it requires certain authorization
            if (requiredAuthorization != null && requiredAuthorization.Any())
            {
                // check roles
                var allRolesValid = true;
                foreach (var role in requiredAuthorization.Roles)
                {
                    // each role now is an OR
                    var hasValidRole = role.Any(r => user?.IsInRole(r) == true);
                    allRolesValid = allRolesValid && hasValidRole;
                    if (!allRolesValid)
                        break;
                }
                if (!allRolesValid)
                    return false;

                return true;
            }
            return true;
        }

        public virtual RequiredAuthorization? GetRequiredAuthFromExpression(LambdaExpression fieldSelection)
        {
            RequiredAuthorization? requiredAuth = null;
            if (fieldSelection.Body.NodeType == ExpressionType.MemberAccess)
            {
                var attributes = ((MemberExpression)fieldSelection.Body).Member.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
                var requiredRoles = attributes.Select(c => c.Roles).Where(r => r != null).ToList();
                requiredAuth = new RequiredAuthorization(requiredRoles!, null);
            }

            return requiredAuth;
        }
        public virtual RequiredAuthorization GetRequiredAuthFromMember(MemberInfo field)
        {
            var attributes = field.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
            var requiredRoles = attributes.Select(c => c.Roles).Where(r => r != null).ToList();
            var requiredAuth = new RequiredAuthorization(requiredRoles!, null);
            return requiredAuth;
        }

        public virtual RequiredAuthorization GetRequiredAuthFromType(Type type)
        {
            var attributes = type.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
            var requiredRoles = attributes.Select(c => c.Roles).Where(r => r != null).ToList();
            var requiredAuth = new RequiredAuthorization(requiredRoles!, null);
            return requiredAuth;
        }
    }
}