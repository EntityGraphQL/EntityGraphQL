using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Provides a way to authenticate a user against a GraphQL request
    /// </summary>
    public interface IGqlAuthorizationService
    {
        RequiredAuthorization? GetRequiredAuthFromExpression(LambdaExpression fieldSelection);
        RequiredAuthorization GetRequiredAuthFromMember(MemberInfo field);
        RequiredAuthorization GetRequiredAuthFromType(Type type);
        bool IsAuthorized(ClaimsPrincipal? user, RequiredAuthorization? requiredAuthorization);
    }
}