using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Authorization;

namespace EntityGraphQL.AspNet;

/// <summary>
/// Checks if the executing user has the required policies to access the requested part of the GraphQL schema
/// </summary>
public class PolicyOrRoleBasedAuthorization : RoleBasedAuthorization
{
    public const string PoliciesKey = "egql:aspnet:policies";

    private readonly IAuthorizationService? authService;

    public PolicyOrRoleBasedAuthorization(IAuthorizationService? authService)
    {
        this.authService = authService;
    }

    /// <summary>
    /// Check if this user has the right security claims, roles or policies to access the request type/field
    /// </summary>
    /// <param name="user">The user to check against</param>
    /// <param name="requiredAuthorization">The required auth for the field or type you want to check against the user</param>
    /// <returns></returns>
    public override bool IsAuthorized(ClaimsPrincipal? user, RequiredAuthorization? requiredAuthorization)
    {
        // A null requiredAuthorization means the field/type has no authorization requirement - open access.
        if (requiredAuthorization == null)
            return true;

        // check policies if any are required
        var policies = requiredAuthorization.GetPolicies();
        if (policies != null && policies.Any())
        {
            // a policy is required but we cannot evaluate it (no authorization service registered or no user) -
            // fail closed rather than silently granting access
            if (authService == null || user == null)
                return false;

            foreach (var policy in policies)
            {
                // each policy entry is an OR-set - at least one must succeed; all entries must pass (AND)
                var hasValidPolicy = policy.Any(p => authService.AuthorizeAsync(user, p).GetAwaiter().GetResult().Succeeded);
                if (!hasValidPolicy)
                    return false;
            }
        }

        // check roles and that the user is authenticated
        return base.IsAuthorized(user, requiredAuthorization);
    }

    private static RequiredAuthorization? GetRequiredAuth(RequiredAuthorization? requiredAuth, ICustomAttributeProvider thing)
    {
        var attributes = thing.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().ToList();
        var requiredRoles = attributes.Where(c => !string.IsNullOrEmpty(c.Roles)).Select(c => c.Roles!.Split(",").ToList()).ToList();
        var requiredPolicies = attributes.Where(c => !string.IsNullOrEmpty(c.Policy)).Select(c => c.Policy!.Split(",").ToList()).ToList();

        // A bare [Authorize] (no roles or policy) still requires an authenticated user - build a present but
        // possibly-empty RequiredAuthorization whenever any [Authorize] attribute exists so it fails closed.
        if (requiredRoles.Count > 0 || requiredPolicies.Count > 0 || attributes.Count > 0)
        {
            var newAuth = new RequiredAuthorization();
            foreach (var roles in requiredRoles)
            {
                newAuth.RequiresAnyRole(roles.ToArray());
            }
            if (requiredPolicies.Count > 0)
            {
                var policyList = new List<List<string>>(requiredPolicies);
                newAuth.SetData(PolicyOrRoleBasedAuthorization.PoliciesKey, policyList);
            }

            if (requiredAuth != null)
                requiredAuth = requiredAuth.Concat(newAuth);
            else
                requiredAuth = newAuth;
        }

        var attributes2 = thing.GetCustomAttributes(typeof(GraphQLAuthorizePolicyAttribute), true).Cast<GraphQLAuthorizePolicyAttribute>();
        var morePolicies = attributes2.Where(c => c.Policies?.Count > 0).Select(c => c.Policies.ToList()).ToList();

        if (morePolicies.Count > 0)
        {
            var policyAuth = new RequiredAuthorization();
            var policyList = new List<List<string>>(morePolicies);
            policyAuth.SetData(PolicyOrRoleBasedAuthorization.PoliciesKey, policyList);

            if (requiredAuth != null)
                requiredAuth = requiredAuth.Concat(policyAuth);
            else
                requiredAuth = policyAuth;
        }

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

    public override RequiredAuthorization? GetRequiredAuthFromMember(MemberInfo field)
    {
        var requiredAuth = base.GetRequiredAuthFromMember(field);
        var authFromAttributes = GetRequiredAuth(requiredAuth, field);

        return authFromAttributes ?? requiredAuth;
    }

    public override RequiredAuthorization? GetRequiredAuthFromType(Type type)
    {
        var requiredAuth = base.GetRequiredAuthFromType(type);
        var authFromAttributes = GetRequiredAuth(requiredAuth, type);

        return authFromAttributes ?? requiredAuth;
    }
}
