using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
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
                var hasValidPolicy = policy.Any(p => CheckPolicy(user, p));
                if (!hasValidPolicy)
                    return false;
            }
        }

        // check roles and that the user is authenticated
        return base.IsAuthorized(user, requiredAuthorization);
    }

    /// <summary>
    /// Evaluate a single policy for the user. The base implementation blocks on the async policy evaluation -
    /// requests going through schema execution avoid this via <see cref="PrepareForRequestAsync"/> which
    /// pre-evaluates the schema's policies asynchronously and answers from those results.
    /// </summary>
    protected virtual bool CheckPolicy(ClaimsPrincipal user, string policy)
    {
        return authService!.AuthorizeAsync(user, policy).GetAwaiter().GetResult().Succeeded;
    }

    /// <summary>
    /// Pre-evaluates every policy used in the schema for this user, properly awaited, and returns a
    /// request-scoped service that answers policy checks from the results. This avoids blocking threads with
    /// sync-over-async policy evaluation during (synchronous) query compilation.
    /// </summary>
    public override async ValueTask<IGqlAuthorizationService> PrepareForRequestAsync(ISchemaProvider schema, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        // no user or no way to evaluate - policy checks fail closed in IsAuthorized, nothing to pre-evaluate
        if (authService == null || user == null)
            return this;

        var policyNames = CollectSchemaPolicies(schema);
        if (policyNames.Count == 0)
            return this;

        var results = new Dictionary<string, bool>(policyNames.Count);
        foreach (var policy in policyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results[policy] = (await authService.AuthorizeAsync(user, policy)).Succeeded;
        }

        return new PreEvaluatedPolicyAuthorization(authService, results);
    }

    private static HashSet<string> CollectSchemaPolicies(ISchemaProvider schema)
    {
        var policyNames = new HashSet<string>();

        void CollectFrom(RequiredAuthorization? auth)
        {
            var policies = auth?.GetPolicies();
            if (policies == null)
                return;
            foreach (var policySet in policies)
                policyNames.UnionWith(policySet);
        }

        var types = schema.GetNonContextTypes().Concat([schema.Type(schema.QueryContextName), schema.Mutation().SchemaType, schema.Subscription().SchemaType]).Distinct();

        foreach (var type in types)
        {
            CollectFrom(type.RequiredAuthorization);
            foreach (var field in type.GetFields())
                CollectFrom(field.RequiredAuthorization);
        }

        return policyNames;
    }

    /// <summary>
    /// Request-scoped authorization answering policy checks from results evaluated up front. A policy not in
    /// the results (e.g. the schema changed mid-request) falls back to the base blocking evaluation.
    /// </summary>
    private sealed class PreEvaluatedPolicyAuthorization : PolicyOrRoleBasedAuthorization
    {
        private readonly IReadOnlyDictionary<string, bool> policyResults;

        public PreEvaluatedPolicyAuthorization(IAuthorizationService authService, IReadOnlyDictionary<string, bool> policyResults)
            : base(authService)
        {
            this.policyResults = policyResults;
        }

        protected override bool CheckPolicy(ClaimsPrincipal user, string policy)
        {
            return policyResults.TryGetValue(policy, out var succeeded) ? succeeded : base.CheckPolicy(user, policy);
        }
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
