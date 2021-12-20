using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Holds authorization info about the user executing the current GraphQL request
    /// </summary>
    public class UserAuthInfo
    {
        private ClaimsPrincipal user;
        private readonly IAuthorizationService authService;

        public UserAuthInfo(ClaimsPrincipal user, IAuthorizationService authService)
        {
            this.user = user;
            this.authService = authService;
        }

        public UserAuthInfo(ClaimsIdentity claims)
        {
            user = new ClaimsPrincipal(claims);
        }

        public IEnumerable<ClaimsIdentity> Indentities
        {
            get => user?.Identities;
        }

        /// <summary>
        /// Check if this user has the right security claims, roles or policies to access the request type/field
        /// </summary>
        /// <param name="requiredAuth">The required auth for the field or type you want to check against the user</param>
        /// <returns></returns>
        internal bool IsAuthorized(RequiredAuthorization requiredAuth)
        {
            // if the list is empty it means identity.IsAuthenticated needs to be true, if full it requires certain authorization
            if (requiredAuth != null && requiredAuth.Any())
            {
                // check polcies if principal with used
                if (authService != null)
                {
                    var allPoliciesValid = true;
                    foreach (var policy in requiredAuth.Policies)
                    {
                        // each policy now is an OR
                        var hasValidPolicy = policy.Any(p => authService.AuthorizeAsync(user, p).Result.Succeeded);
                        allPoliciesValid = allPoliciesValid && hasValidPolicy;
                        if (!allPoliciesValid)
                            break;
                    }
                    if (!allPoliciesValid)
                        return false;
                }

                // check roles
                var allRolesValid = true;
                foreach (var role in requiredAuth.Roles)
                {
                    // each role now is an OR
                    var hasValidRole = role.Any(r => user.IsInRole(r));
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
    }
}