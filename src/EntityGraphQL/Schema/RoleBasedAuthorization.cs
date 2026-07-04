using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using EntityGraphQL.Authorization;

namespace EntityGraphQL.Schema;

/// <summary>
/// Checks if the executing user has the required roles to access the requested part of the GraphQL schema
/// </summary>
public class RoleBasedAuthorization : IGqlAuthorizationService
{
    public RoleBasedAuthorization() { }

    /// <summary>
    /// Check if this user has the right security claims, roles or policies to access the request type/field
    /// </summary>
    /// <param name="requiredAuthorization">The required auth for the field or type you want to check against the user</param>
    /// <returns></returns>
    public virtual bool IsAuthorized(ClaimsPrincipal? user, RequiredAuthorization? requiredAuthorization)
    {
        // A null requiredAuthorization means the field/type has no authorization requirement - open access.
        if (requiredAuthorization == null)
            return true;

        // Authorization IS required. At a minimum the user must be authenticated - an empty (but present)
        // RequiredAuthorization (e.g. a bare [GraphQLAuthorize]) means "any authenticated user".
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        // Then satisfy any role requirements. Each entry is an AND of an OR-set of roles.
        var roles = requiredAuthorization.GetRoles();
        if (roles != null)
        {
            foreach (var role in roles)
            {
                if (!role.Any(r => user.IsInRole(r)))
                    return false;
            }
        }

        return true;
    }

    public virtual RequiredAuthorization? GetRequiredAuthFromExpression(LambdaExpression fieldSelection)
    {
        if (fieldSelection.Body.NodeType == ExpressionType.MemberAccess)
        {
            var attributes = ((MemberExpression)fieldSelection.Body).Member.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
            return BuildRequiredAuth(attributes);
        }
        return null;
    }

    public virtual RequiredAuthorization? GetRequiredAuthFromMember(MemberInfo field)
    {
        var attributes = field.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
        return BuildRequiredAuth(attributes);
    }

    public virtual RequiredAuthorization? GetRequiredAuthFromType(Type type)
    {
        var attributes = type.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
        return BuildRequiredAuth(attributes);
    }

    /// <summary>
    /// Builds a RequiredAuthorization from any GraphQLAuthorize attributes present. Returns null when there are
    /// none (open access). When at least one attribute is present the result is non-null so authorization is
    /// enforced even for a bare [GraphQLAuthorize] with no roles (meaning "any authenticated user").
    /// </summary>
    protected static RequiredAuthorization? BuildRequiredAuth(IEnumerable<GraphQLAuthorizeAttribute> attributes)
    {
        var attributeList = attributes.ToList();
        if (attributeList.Count == 0)
            return null;

        var auth = new RequiredAuthorization();
        foreach (var roles in attributeList.Select(c => c.Roles).Where(r => r != null))
        {
            auth.RequiresAnyRole(roles!.ToArray());
        }
        return auth;
    }
}
