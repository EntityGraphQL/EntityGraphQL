using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace EntityGraphQL.Schema;

/// <summary>
/// Provides a way to authenticate a user against a GraphQL request
/// </summary>
public interface IGqlAuthorizationService
{
    RequiredAuthorization? GetRequiredAuthFromExpression(LambdaExpression fieldSelection);
    RequiredAuthorization? GetRequiredAuthFromMember(MemberInfo field);
    RequiredAuthorization? GetRequiredAuthFromType(Type type);
    bool IsAuthorized(ClaimsPrincipal? user, RequiredAuthorization? requiredAuthorization);

    /// <summary>
    /// Called at the start of each request, in an async context, before any (synchronous) IsAuthorized calls
    /// are made while compiling the query. Implementations that need async work to answer IsAuthorized (e.g.
    /// evaluating ASP.NET Core authorization policies) can do it here and return a request-scoped service that
    /// answers from the results, avoiding sync-over-async during compilation. The default returns the service
    /// unchanged.
    /// </summary>
    ValueTask<IGqlAuthorizationService> PrepareForRequestAsync(ISchemaProvider schema, ClaimsPrincipal? user, CancellationToken cancellationToken = default) =>
        new(this);
}
