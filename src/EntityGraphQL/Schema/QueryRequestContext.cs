using System.Security.Claims;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Holds information about the user executing the current GraphQL request
    /// </summary>
    public class QueryRequestContext
    {

        public QueryRequestContext(QueryRequest query, IGqlAuthorizationService? authorizationService, ClaimsPrincipal? user)
        {
            Query = query;
            AuthorizationService = authorizationService;
            User = user;
        }

        public QueryRequest Query { get; }
        public IGqlAuthorizationService? AuthorizationService { get; }
        public ClaimsPrincipal? User { get; }
    }
}