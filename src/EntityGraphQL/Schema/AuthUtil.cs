using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace EntityGraphQL.Schema
{
    public static class AuthUtil
    {
        /// <summary>
        /// Check if this field required certain security claims and if so check against the ClaimsIdentity
        /// </summary>
        /// <param name="claims"></param>
        /// <returns></returns>
        public static bool IsAuthorized(ClaimsIdentity claims, IEnumerable<string> authorizeClaims)
        {
            // if the list is empty it means claims.IsAuthenticated needs to be true, if full it requires certain claims
            if (authorizeClaims != null)
            {
                if (claims.IsAuthenticated && (!authorizeClaims.Any() || authorizeClaims.Where(a => claims.HasClaim(ClaimTypes.Role, a)).Any()))
                {
                    return true;
                }
                return false;
            }
            return true;
        }
    }
}