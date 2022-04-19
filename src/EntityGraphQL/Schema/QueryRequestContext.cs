using System.Security.Claims;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Holds information about the user executing the current GraphQL request
    /// </summary>
    public class QueryRequestContext
    {

        public QueryRequestContext(IGqlAuthorizationService? authorizationService, ClaimsPrincipal? user)
        {
            AuthorizationService = authorizationService;
            User = user;
        }

        public IGqlAuthorizationService? AuthorizationService { get; }
        public ClaimsPrincipal? User { get; }
    }
}