using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using EntityGraphQL.Authorization;

namespace EntityGraphQL.Schema
{
    public class RequiredClaims
    {
        // each item in the "first" list is AND claims and each in the inner list is OR claims
        private readonly List<List<string>> requiredClaims;
        public IEnumerable<IEnumerable<string>> Claims { get => requiredClaims; }

        public RequiredClaims()
        {
            requiredClaims = new List<List<string>>();
        }

        public RequiredClaims(IEnumerable<GraphQLAuthorizeAttribute> claims)
        {
            requiredClaims = claims.Select(c => c.Claims).ToList();
        }

        public bool Any() => requiredClaims.Any();

        public bool HasRequired(ClaimsIdentity claims)
        {
            // each item in the "first" list is AND claims and each in the inner list is OR claims
            return requiredClaims.All(andClaim => andClaim.Any(orClaim => claims.HasClaim(ClaimTypes.Role, orClaim)));
        }

        public void RequiresAllClaims(string[] claims)
        {
            requiredClaims.AddRange(claims.Select(s => new List<string> { s }));
        }

        public void RequiresAnyClaim(string[] claims)
        {
            requiredClaims.Add(claims.ToList());
        }

        internal void Add(IEnumerable<GraphQLAuthorizeAttribute> attributes)
        {
            requiredClaims.AddRange(attributes.Select(c => c.Claims).ToList());
        }
    }
    public static class AuthUtil
    {
        /// <summary>
        /// Check if this field required certain security claims and if so check against the ClaimsIdentity
        /// </summary>
        /// <param name="claims"></param>
        /// <returns></returns>
        public static bool IsAuthorized(ClaimsIdentity claims, RequiredClaims authorizeClaims)
        {
            // if the list is empty it means claims.IsAuthenticated needs to be true, if full it requires certain claims
            if (authorizeClaims != null && claims != null && authorizeClaims.Any())
            {
                if (claims.IsAuthenticated && authorizeClaims.HasRequired(claims))
                {
                    return true;
                }
                return false;
            }
            return true;
        }
    }
}