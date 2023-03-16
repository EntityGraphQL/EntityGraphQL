using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Authorization;

namespace EntityGraphQL.AspNet
{
    /// <summary>
    /// Checks if the executing user has the required policies to access the requested part of the GraphQL schema
    /// </summary>
    public class PolicyOrRoleBasedAuthorization : RoleBasedAuthorization
    {
        private readonly IAuthorizationService? authService;

        public PolicyOrRoleBasedAuthorization(IAuthorizationService? authService)
        {
            this.authService = authService;
        }

        /// <summary>
        /// Check if this user has the right security claims, roles or policies to access the request type/field
        /// </summary>
        /// <param name="requiredAuthorization">The required auth for the field or type you want to check against the user</param>
        /// <returns></returns>
        public override bool IsAuthorized(ClaimsPrincipal? user, RequiredAuthorization? requiredAuthorization)
        {
            // if the list is empty it means identity.IsAuthenticated needs to be true, if full it requires certain authorization
            if (requiredAuthorization != null && requiredAuthorization.Any())
            {
                // check polices if principal with used
                if (authService != null && user != null)
                {
                    var allPoliciesValid = true;
                    foreach (var policy in requiredAuthorization.Policies)
                    {
                        // each policy now is an OR
                        var hasValidPolicy = policy.Any(p => authService.AuthorizeAsync(user, p).GetAwaiter().GetResult().Succeeded);
                        allPoliciesValid = allPoliciesValid && hasValidPolicy;
                        if (!allPoliciesValid)
                            break;
                    }
                    if (!allPoliciesValid)
                        return false;
                }

                // check roles
                return base.IsAuthorized(user, requiredAuthorization);
            }
            return true;
        }

        private static RequiredAuthorization GetRequiredAuth(RequiredAuthorization? requiredAuth, ICustomAttributeProvider thing)
        {
            var attributes = thing.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
            var requiredRoles = attributes.Where(c => !string.IsNullOrEmpty(c.Roles)).Select(c => c.Roles!.Split(",").ToList()).ToList();
            var requiredPolicies = attributes.Where(c => !string.IsNullOrEmpty(c.Policy)).Select(c => c.Policy!.Split(",").ToList()).ToList();
            var newAuth = new RequiredAuthorization(requiredRoles, requiredPolicies);
            if (requiredAuth != null)
                requiredAuth = requiredAuth.Concat(newAuth);
            else
                requiredAuth = newAuth;

            var attributes2 = thing.GetCustomAttributes(typeof(GraphQLAuthorizePolicyAttribute), true).Cast<GraphQLAuthorizePolicyAttribute>();

            requiredPolicies = attributes2.Where(c => c.Policies?.Any() == true).Select(c => c.Policies.ToList()).ToList();
            requiredAuth = requiredAuth.Concat(new RequiredAuthorization(null, requiredPolicies));
            return requiredAuth;
        }

        public override RequiredAuthorization? GetRequiredAuthFromExpression(LambdaExpression fieldSelection)
        {
            var requiredAuth = base.GetRequiredAuthFromExpression(fieldSelection);
            if (fieldSelection.Body.NodeType == ExpressionType.MemberAccess)
            {
                requiredAuth = GetRequiredAuth(requiredAuth, ((MemberExpression)fieldSelection.Body).Member);
            }

            return requiredAuth;
        }

        public override RequiredAuthorization GetRequiredAuthFromMember(MemberInfo field)
        {
            var requiredAuth = base.GetRequiredAuthFromMember(field);
            requiredAuth = GetRequiredAuth(requiredAuth, field);

            return requiredAuth;
        }

        public override RequiredAuthorization GetRequiredAuthFromType(Type type)
        {
            var requiredAuth = base.GetRequiredAuthFromType(type);
            requiredAuth = GetRequiredAuth(requiredAuth, type);

            return requiredAuth;
        }
    }
}